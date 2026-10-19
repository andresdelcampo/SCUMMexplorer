using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// A booted session kept as it was, from which every fresh-start run begins (cutscene snapshot, 18 October 2026): instead of booting the game once per
/// run, the boot is done once and each run gets a deep copy of it (<see cref="Start"/>). The copy is the whole object graph the session owns: variables
/// and bits, script slots with their instruction pointers and locals, the cutscene and override stacks, actors, objects and their states, the loaded
/// room, camera, verbs, messages, the sound clock and music timelines, the seeded random generator and its picks, pending input, the trace, and every
/// per-session cache (the data source's own caches among them), so a run from a copy does what a run after its own boot does.
/// <para>
/// Shared, never copied: the game (<see cref="GameLibrary"/>) and everything it reaches (its archives, disks, disc image and speech bank: the readers each
/// worker already owns, as before), strings, reflection objects and objects without fields. The snapshot itself is never run; copies may be made from
/// several threads at once (a copy only reads it), and each copy belongs to one thread.
/// </para>
/// </summary>
public sealed class ScummBootSnapshot
{
    private readonly ScummGameSession _frozen;
    private readonly HashSet<object> _shared;

    private ScummBootSnapshot(ScummGameSession frozen, HashSet<object> shared)
    {
        _frozen = frozen;
        _shared = shared;
    }

    /// <summary>Keeps a copy of the session as it is now; the session itself may go on being used.</summary>
    public static ScummBootSnapshot Of(ScummGameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var shared = ObjectGraphCopy.Reachable(session.SessionGame);
        return new(ObjectGraphCopy.Copy(session, shared), shared);
    }

    /// <summary>A new session in the snapshot's state, independent of the snapshot and of every other copy.</summary>
    public ScummGameSession Start() => ObjectGraphCopy.Copy(_frozen, _shared);

    /// <summary>
    /// A factory of fresh sessions that boots once (the first call, through <paramref name="boot"/>) and gives a copy of that boot every time after.
    /// Thread-safe: the boot runs once, and copies may be made on several threads. For games whose readers may be shared between threads.
    /// </summary>
    public static Func<ScummGameSession> Shared(Func<ScummGameSession> boot)
    {
        ArgumentNullException.ThrowIfNull(boot);
        var snapshot = new Lazy<ScummBootSnapshot>(() => Of(boot()), LazyThreadSafetyMode.ExecutionAndPublication);
        return () => snapshot.Value.Start();
    }

}

/// <summary>
/// Snapshots for runs that may not share a reader (a disk or archive game, where each boot reads its own copy of the game): a run takes a free snapshot,
/// or boots a new one when every snapshot is in use, and gives it back when it ends. So there are at most as many boots as runs going on at once, and no
/// snapshot (nor its copy of the game) serves two runs at the same time, whichever threads the runs are on.
/// </summary>
public sealed class ScummBootSnapshotPool(Func<ScummGameSession> boot)
{
    private readonly ConcurrentBag<ScummBootSnapshot> _free = [];
    private int _boots;

    /// <summary>The boots made so far.</summary>
    public int Boots => _boots;

    /// <summary>Runs <paramref name="run"/> with a factory of fresh sessions copied from a snapshot that nothing else uses meanwhile.</summary>
    public T Use<T>(Func<Func<ScummGameSession>, T> run)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (!_free.TryTake(out var snapshot))
        {
            snapshot = ScummBootSnapshot.Of(boot());
            Interlocked.Increment(ref _boots);
        }
        try { return run(snapshot.Start); }
        finally { _free.Add(snapshot); }
    }
}

/// <summary>
/// A deep copy of an object graph by its fields (reference identity and cycles kept), with a set of objects shared instead of copied. Hash-based
/// collections whose keys hash by identity are rebuilt with the copied keys (a key's copy has another identity hash); every other object is copied
/// field by field, so lists, stacks, queues and dictionaries keep their exact internal order.
/// </summary>
internal static class ObjectGraphCopy
{
    private static readonly Func<object, object> ShallowClone = (Func<object, object>)Delegate.CreateDelegate(typeof(Func<object, object>),
        typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!);

    private static readonly ConcurrentDictionary<Type, TypePlan> Plans = new();

    private sealed record TypePlan(Kind Kind, FieldInfo[] Fields, bool ElementsNeedCopy, bool HashCollection);

    private enum Kind { Leaf, Object, Array, Delegate, Refused }

    /// <summary>Every object reachable from the root (by fields), the root included; strings and leaves are not listed.</summary>
    public static HashSet<object> Reachable(object? root)
    {
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var work = new Stack<object>();
        if (root is not null) work.Push(root);
        while (work.Count > 0)
        {
            var item = work.Pop();
            var type = item.GetType();
            var plan = PlanFor(type);
            if (plan.Kind is Kind.Leaf || !seen.Add(item)) continue;
            switch (plan.Kind)
            {
                case Kind.Array when plan.ElementsNeedCopy:
                    foreach (var element in (Array)item) if (element is not null) work.Push(element);
                    break;
                case Kind.Delegate:
                    foreach (var call in ((Delegate)item).GetInvocationList()) if (call.Target is not null) work.Push(call.Target);
                    break;
                case Kind.Object or Kind.Refused:
                    foreach (var field in plan.Fields) PushValue(field.GetValue(item), work);
                    break;
            }
        }
        return seen;

        static void PushValue(object? value, Stack<object> work)
        {
            if (value is null) return;
            if (value.GetType().IsValueType)
                foreach (var field in PlanFor(value.GetType()).Fields) PushValue(field.GetValue(value), work);
            else work.Push(value);
        }
    }

    public static T Copy<T>(T root, HashSet<object> shared) where T : class
    {
        var copier = new Copier(shared);
        var copy = (T)copier.CopyReference(root)!;
        copier.Finish();
        return copy;
    }

    private sealed class Copier(HashSet<object> shared)
    {
        private readonly Dictionary<object, object> _copies = new(ReferenceEqualityComparer.Instance);
        private readonly Queue<(object Copy, TypePlan Plan)> _work = new();
        private readonly List<object> _hashCollections = [];

        public object? CopyReference(object? original)
        {
            if (original is null || shared.Contains(original)) return original;
            if (_copies.TryGetValue(original, out var known)) return known;
            var plan = PlanFor(original.GetType());
            switch (plan.Kind)
            {
                case Kind.Leaf:
                    return original;
                case Kind.Refused:
                    throw new NotSupportedException($"A session snapshot cannot copy a {original.GetType().FullName} (an operating-system or threading resource) outside the game's readers.");
                case Kind.Delegate:
                {
                    Delegate? result = null;
                    _copies[original] = null!; // A delegate's target never reaches the delegate itself in a session.
                    foreach (var call in ((Delegate)original).GetInvocationList())
                    {
                        var target = CopyReference(call.Target);
                        var copy = call.Target is null ? Delegate.CreateDelegate(call.GetType(), call.Method) : Delegate.CreateDelegate(call.GetType(), target, call.Method);
                        result = Delegate.Combine(result, copy);
                    }
                    _copies[original] = result!;
                    return result;
                }
                default:
                {
                    var copy = plan.Kind == Kind.Array ? ((Array)original).Clone() : ShallowClone(original);
                    _copies[original] = copy;
                    if (plan.Kind == Kind.Object && plan.Fields.Length > 0 || plan.Kind == Kind.Array && plan.ElementsNeedCopy) _work.Enqueue((copy, plan));
                    if (plan.HashCollection) _hashCollections.Add(copy);
                    return copy;
                }
            }
        }

        /// <summary>Copies a value of a struct type that holds references: its referenced objects are copies, the value itself a new box.</summary>
        private object? CopyValue(object? value)
        {
            if (value is null) return null;
            var type = value.GetType();
            if (!type.IsValueType) return CopyReference(value);
            var plan = PlanFor(type);
            if (plan.Fields.Length == 0) return value;
            // GetValue gave a box of its own, so it can be filled in place.
            foreach (var field in plan.Fields) field.SetValue(value, CopyValue(field.GetValue(value)));
            return value;
        }

        public void Finish()
        {
            while (_work.Count > 0)
            {
                var (copy, plan) = _work.Dequeue();
                if (plan.Kind == Kind.Array)
                {
                    var array = (Array)copy;
                    if (array.Rank == 1 && copy is object?[] references)
                        for (var i = 0; i < references.Length; i++) references[i] = CopyReference(references[i]);
                    else
                    {
                        var index = 0;
                        foreach (var element in array)
                        {
                            SetFlat(array, index, CopyValue(element));
                            index++;
                        }
                    }
                    continue;
                }
                if (copy.GetType().IsValueType)
                {
                    // A boxed struct reached through an object-typed field: its box is the copy, filled in place.
                    foreach (var field in plan.Fields) field.SetValue(copy, CopyValue(field.GetValue(copy)));
                    continue;
                }
                foreach (var field in plan.Fields) field.SetValue(copy, CopyValue(field.GetValue(copy)));
            }
            foreach (var collection in _hashCollections) Rehash(collection);
        }

        private static void SetFlat(Array array, int index, object? value)
        {
            if (array.Rank == 1) { array.SetValue(value, index); return; }
            var indices = new int[array.Rank];
            for (var dimension = array.Rank - 1; dimension >= 0; dimension--)
            {
                var length = array.GetLength(dimension);
                indices[dimension] = index % length + array.GetLowerBound(dimension);
                index /= length;
            }
            array.SetValue(value, indices);
        }

        /// <summary>
        /// A dictionary or set whose keys' hash codes changed with the copy (keys hashed by identity) is rebuilt from its pairs in their order; one whose
        /// keys hash by content keeps its copied buckets.
        /// </summary>
        private static void Rehash(object collection)
        {
            var type = collection.GetType();
            var comparer = type.GetProperty("Comparer")!.GetValue(collection)!;
            var hash = comparer.GetType().GetInterfaces().First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEqualityComparer<>))
                .GetMethod("GetHashCode")!;
            var isDictionary = type.GetGenericTypeDefinition() == typeof(Dictionary<,>);
            var keyType = type.GetGenericArguments()[0];
            // Numbers, strings, enums and tuples of them hash the same in the copy (same content, same comparer).
            if (HashesByContent(keyType) && comparer is not ReferenceEqualityComparer) return;
            var entries = new List<(object Key, object? Value)>();
            if (isDictionary)
            {
                var pairs = ((IDictionary)collection).GetEnumerator();
                while (pairs.MoveNext()) entries.Add((pairs.Key, pairs.Value));
            }
            else foreach (var key in (IEnumerable)collection) entries.Add((key!, null));
            if (entries.Count == 0) return;
            // Otherwise the stored hash codes (Dictionary: Entry.hashCode, HashSet: Entry.HashCode) must equal the copied keys' hashes.
            const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase;
            var stored = type.GetField("_entries", Any)?.GetValue(collection) as Array;
            var entryType = stored?.GetType().GetElementType();
            var hashField = entryType?.GetField("hashCode", Any);
            var keyField = entryType?.GetField(isDictionary ? "key" : "value", Any);
            var nextField = entryType?.GetField("next", Any);
            var consistent = stored is not null && hashField is not null && keyField is not null && nextField is not null;
            if (consistent)
            {
                var count = (int)type.GetField("_count", Any)!.GetValue(collection)!;
                for (var i = 0; i < count && consistent; i++)
                {
                    var entry = stored!.GetValue(i)!;
                    if ((int)nextField!.GetValue(entry)! < -1) continue; // A free slot.
                    var key = keyField!.GetValue(entry);
                    var expected = key is null ? 0 : (int)hash.Invoke(comparer, [key])!;
                    consistent = unchecked((int)Convert.ToInt64(hashField!.GetValue(entry))) == expected;
                }
            }
            if (consistent) return;
            type.GetMethod("Clear")!.Invoke(collection, null);
            var add = type.GetMethod(isDictionary ? "Add" : "Add", isDictionary ? [keyType, type.GetGenericArguments()[1]] : [keyType])!;
            foreach (var (key, value) in entries) add.Invoke(collection, isDictionary ? [key, value] : [key]);
        }
    }

    private static bool HashesByContent(Type type)
    {
        if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal)) return true;
        if (Nullable.GetUnderlyingType(type) is { } inner) return HashesByContent(inner);
        if (type.IsGenericType && type.FullName!.StartsWith("System.ValueTuple`", StringComparison.Ordinal)) return type.GetGenericArguments().All(HashesByContent);
        return false;
    }

    private static TypePlan PlanFor(Type type) => Plans.GetOrAdd(type, NewPlan);

    private static TypePlan NewPlan(Type type)
    {
        if (IsLeaf(type)) return new(Kind.Leaf, [], false, false);
        if (typeof(Delegate).IsAssignableFrom(type)) return new(Kind.Delegate, [], false, false);
        if (type.IsArray)
        {
            var element = type.GetElementType()!;
            return new(Kind.Array, [], !IsLeaf(element) && (!element.IsValueType || HoldsReferences(element)), false);
        }
        if (IsRefused(type)) return new(Kind.Refused, InstanceFields(type).ToArray(), false, false);
        var fields = InstanceFields(type).Where(f => !IsLeaf(f.FieldType) && (!f.FieldType.IsValueType || HoldsReferences(f.FieldType))).ToArray();
        // A reference type without any field is stateless (a comparer, a lambda cache): shared.
        if (!type.IsValueType && !InstanceFields(type).Any()) return new(Kind.Leaf, [], false, false);
        var hashCollection = type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(Dictionary<,>) || type.GetGenericTypeDefinition() == typeof(HashSet<>));
        if (type.IsGenericType && type.GetGenericTypeDefinition() is var definition &&
            (definition == typeof(ConcurrentDictionary<,>) || definition == typeof(ConditionalWeakTable<,>)) || type == typeof(Hashtable))
            return new(Kind.Refused, InstanceFields(type).ToArray(), false, false);
        return new(Kind.Object, fields, false, hashCollection);
    }

    private static IEnumerable<FieldInfo> InstanceFields(Type type)
    {
        for (var t = type; t is not null && t != typeof(object); t = t.BaseType)
            foreach (var field in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                yield return field;
    }

    private static bool IsLeaf(Type type) =>
        type.IsPrimitive || type.IsEnum || type.IsPointer || type == typeof(string) || type == typeof(decimal) || type == typeof(DateTime) ||
        type == typeof(TimeSpan) || type == typeof(Guid) || typeof(MemberInfo).IsAssignableFrom(type) || typeof(Assembly).IsAssignableFrom(type) ||
        typeof(Module).IsAssignableFrom(type) || typeof(System.Text.Encoding).IsAssignableFrom(type) || typeof(System.Globalization.CultureInfo).IsAssignableFrom(type) ||
        typeof(StringComparer).IsAssignableFrom(type) || type == typeof(IntPtr) || type == typeof(UIntPtr) ||
        type.IsValueType && !HoldsReferences(type);

    private static bool IsRefused(Type type) =>
        typeof(Stream).IsAssignableFrom(type) || typeof(System.Runtime.InteropServices.SafeHandle).IsAssignableFrom(type) || typeof(Thread).IsAssignableFrom(type) ||
        typeof(Task).IsAssignableFrom(type) || typeof(WaitHandle).IsAssignableFrom(type) || typeof(CancellationTokenSource).IsAssignableFrom(type) ||
        type == typeof(SemaphoreSlim) || type == typeof(ReaderWriterLockSlim);

    private static readonly ConcurrentDictionary<Type, bool> ReferenceHolders = new();

    /// <summary>Whether a struct type holds a reference somewhere in its fields (a struct of numbers alone is copied with its parent).</summary>
    private static bool HoldsReferences(Type type)
    {
        if (!type.IsValueType) return true;
        if (type.IsPrimitive || type.IsEnum || type.IsPointer) return false;
        return ReferenceHolders.GetOrAdd(type, t => InstanceFields(t).Any(f => !f.FieldType.IsPointer && (!f.FieldType.IsValueType || HoldsReferences(f.FieldType))));
    }
}

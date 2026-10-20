using System.Collections;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// A bounded script run's variables: the globals as the run sees them, its 26 locals and its own keys.
/// <para>
/// Until 19 October 2026 every run copied every global into its own dictionary at its start (800 to 1400 entries), copied the globals back
/// before each event and at its end, and copied the runtime's globals in again after each event. This class gives the same reads, writes,
/// enumeration order and copies without the full copies (globals change tracking, 20 October 2026):
/// </para>
/// <list type="bullet">
/// <item>Through the store (a run whose result does not keep its variables): the run reads the globals as they were at its last copy point from
/// the runtime's <see cref="ScummGlobalVariables"/> (the store's record of writes gives a value changed since), keeps its own writes apart, and
/// at a copy point writes back what it changed (and puts back what the runtime changed outside an event, as the full copy did).</item>
/// <item>Its own dictionary (every other run, and a run through the store from the moment a removal would change the order the full copy had):
/// exactly the old behaviour.</item>
/// </list>
/// </summary>
internal sealed class ScummRunVariables : IDictionary<string, ushort>, IReadOnlyDictionary<string, ushort>
{
    private Dictionary<string, ushort>? _full;

    // Through the store. The objects are reused per thread (a session runs a few dozen scripts a frame).
    [ThreadStatic] private static Stack<ScummRunVariables>? _pool;
    private ScummGlobalVariables? _store;
    private readonly Dictionary<string, ushort> _overlay = new(StringComparer.Ordinal);
    private readonly List<string> _initial = [];
    private readonly List<string> _appended = [];
    private readonly Dictionary<string, int> _appendedIndex = new(StringComparer.Ordinal);
    private long _startAdds;
    private int _scanned;
    private readonly Dictionary<string, (bool Existed, ushort Old)> _atSync = new(StringComparer.Ordinal);

    /// <summary>The run's own dictionary (the old behaviour).</summary>
    public ScummRunVariables(Dictionary<string, ushort> full) => _full = full;

    private ScummRunVariables() { }

    /// <summary>Through the store: the run's first keys after the globals (<paramref name="initial"/>, in order) with their values.</summary>
    public static ScummRunVariables Track(ScummGlobalVariables store, IReadOnlyList<(string Key, ushort Value)> initial)
    {
        var run = _pool is { Count: > 0 } pool ? pool.Pop() : new ScummRunVariables();
        run.Start(store, initial);
        return run;
    }

    /// <summary>A run through the store has ended and nothing holds these variables: the object serves a later run on this thread.</summary>
    public void Release()
    {
        if (_store is null) return;
        _store.UnregisterFastRun(this);
        _store = null;
        _full = null;
        _overlay.Clear();
        _initial.Clear();
        _appended.Clear();
        _appendedIndex.Clear();
        _atSync.Clear();
        (_pool ??= new()).Push(this);
    }

    private void Start(ScummGlobalVariables store, IReadOnlyList<(string Key, ushort Value)> initial)
    {
        _store = store;
        _scanned = store.Journal.Count;
        _startAdds = store.AddSequence;
        foreach (var (key, value) in initial)
        {
            _initial.Add(key);
            _overlay[key] = value;
        }
        store.RegisterFastRun(this);
    }

    /// <summary>Whether a run can read through the store: none of the run's own keys is a global (the full copy would have put them first).</summary>
    public static bool CanTrack(ScummGlobalVariables store, IEnumerable<string> ownKeys) =>
        !FullCopiesOnThisThread && store.LocalKeyCount == 0 && !ownKeys.Any(store.ContainsKey);

    /// <summary>For tests: runs on this thread copy every global, as before the tracking, so both ways can be compared.</summary>
    [ThreadStatic] internal static bool FullCopiesOnThisThread;

    public bool IsTracking => _full is null;

    /// <summary>The run's own dictionary, when it has one.</summary>
    public Dictionary<string, ushort>? Full => _full;

    private static bool IsGlobalKey(string key) =>
        key.StartsWith("var[", StringComparison.Ordinal) || key.StartsWith("bit[", StringComparison.Ordinal);

    private void Scan()
    {
        var journal = _store!.Journal;
        while (_scanned < journal.Count)
        {
            var change = journal[_scanned++];
            _atSync.TryAdd(change.Key, (change.Existed, change.Old));
        }
    }

    private bool TryGetGlobalAtSync(string key, out ushort value)
    {
        if (_scanned < _store!.Journal.Count) Scan();
        if (_atSync.Count > 0 && _atSync.TryGetValue(key, out var before))
        {
            value = before.Old;
            return before.Existed;
        }
        return _store.TryGetValue(key, out value);
    }

    private void Resync()
    {
        _scanned = _store!.Journal.Count;
        _atSync.Clear();
    }

    public bool TryGetValue(string key, out ushort value)
    {
        if (_full is not null) return _full.TryGetValue(key, out value);
        if (_overlay.TryGetValue(key, out value)) return true;
        return TryGetGlobalAtSync(key, out value);
    }

    public bool ContainsKey(string key) => TryGetValue(key, out _);

    public ushort this[string key]
    {
        get => TryGetValue(key, out var value) ? value : throw new KeyNotFoundException($"The given key '{key}' was not present in the dictionary.");
        set
        {
            if (_full is not null) { _full[key] = value; return; }
            if (!_overlay.ContainsKey(key) && !TryGetGlobalAtSync(key, out _))
            {
                _appendedIndex.Add(key, _appended.Count);
                _appended.Add(key);
            }
            _overlay[key] = value;
        }
    }

    public void Add(string key, ushort value)
    {
        if (_full is not null) { _full.Add(key, value); return; }
        if (ContainsKey(key)) throw new ArgumentException($"An item with the same key has already been added. Key: {key}");
        this[key] = value;
    }

    public bool Remove(string key)
    {
        Materialize();
        return _full!.Remove(key);
    }

    public void Clear()
    {
        Materialize();
        _full!.Clear();
    }

    /// <summary>
    /// Fills <paramref name="destination"/> in the order of the run's old dictionary: the globals the run started with (the store's order, unchanged
    /// while no removal happened), the run's first keys, then the keys added since, in the order they came.
    /// </summary>
    private void CopyInto(Dictionary<string, ushort> destination)
    {
        if (_full is not null)
        {
            foreach (var (key, value) in _full) destination.Add(key, value);
            return;
        }
        var store = _store!;
        if (_scanned < store.Journal.Count) Scan();
        var anyChanged = _atSync.Count > 0;
        for (var slot = 0; slot < store.SlotCount; slot++)
        {
            if (store.KeyAt(slot) is not { } key || store.AddedAt(slot) >= _startAdds) continue;
            if (_overlay.TryGetValue(key, out var value)) { destination.Add(key, value); continue; }
            if (anyChanged && _atSync.TryGetValue(key, out var before))
            {
                if (before.Existed) destination.Add(key, before.Old);
                continue;
            }
            destination.Add(key, store.ValueAt(slot));
        }
        foreach (var key in _initial)
            if (TryGetValue(key, out var value)) destination.Add(key, value);
        foreach (var key in _appended)
            if (TryGetValue(key, out var value)) destination.Add(key, value);
    }

    /// <summary>A new dictionary equal to the run's variables, in their order (what <c>new Dictionary(variables)</c> gave).</summary>
    public Dictionary<string, ushort> Snapshot()
    {
        var copy = new Dictionary<string, ushort>(_full?.Count ?? _store!.Count + _initial.Count + _appended.Count, StringComparer.Ordinal);
        CopyInto(copy);
        return copy;
    }

    private List<KeyValuePair<string, ushort>> Ordered() => [.. Snapshot()];

    /// <summary>From here on the run has its own dictionary, filled as the full copies would have left it.</summary>
    public void Materialize()
    {
        if (_full is not null) return;
        _full = Snapshot();
        _store!.UnregisterFastRun(this);
    }
    /// <summary>Before an event and at the end: every global of the run goes to the runtime (only var[ and bit[ keys).</summary>
    public void CopyGlobalsTo(ScummGlobalVariables runtime)
    {
        if (_full is null)
        {
            Scan();
            // A global the runtime added since the last copy point, or a non-global it changed, is beyond what the tracking reproduces.
            foreach (var (key, before) in _atSync)
                if (!before.Existed || (!IsGlobalKey(key) && !_overlay.ContainsKey(key))) { Materialize(); break; }
        }
        if (_full is not null)
        {
            foreach (var (key, value) in _full)
                if (IsGlobalKey(key)) runtime[key] = value;
            return;
        }
        // What the runtime changed outside an event goes back to the value the run holds (the run's copy overwrote it).
        foreach (var (key, before) in _atSync)
            if (IsGlobalKey(key) && !_overlay.ContainsKey(key)) runtime[key] = before.Old;
        // New keys in the order the run added them (the order of the full copy), then the run's other writes.
        foreach (var key in _appended)
            if (IsGlobalKey(key) && !runtime.ContainsKey(key) && _overlay.TryGetValue(key, out var value)) runtime[key] = value;
        List<string>? written = null;
        foreach (var (key, value) in _overlay)
            if (IsGlobalKey(key)) { runtime[key] = value; (written ??= []).Add(key); }
        if (written is not null) foreach (var key in written) _overlay.Remove(key);
        Resync();
    }

    /// <summary>After an event: every global of the runtime comes into the run (its new keys in the runtime's order).</summary>
    public void CopyGlobalsFrom(ScummGlobalVariables runtime)
    {
        if (_full is not null)
        {
            foreach (var (key, value) in runtime) _full[key] = value;
            return;
        }
        Scan();
        List<string>? added = null;
        foreach (var (key, before) in _atSync)
            if (!before.Existed && !_overlay.ContainsKey(key) && !_appendedIndex.ContainsKey(key) && runtime.ContainsKey(key)) (added ??= []).Add(key);
        if (added is not null)
        {
            if (added.Count > 1) added.Sort((a, b) => runtime.SlotOf(a).CompareTo(runtime.SlotOf(b)));
            foreach (var key in added)
            {
                _appendedIndex.Add(key, _appended.Count);
                _appended.Add(key);
            }
        }
        List<string>? shadowed = null;
        foreach (var key in _overlay.Keys)
            if (runtime.ContainsKey(key)) (shadowed ??= []).Add(key);
        if (shadowed is not null) foreach (var key in shadowed) _overlay.Remove(key);
        Resync();
    }

    /// <summary>The run's locals (keys starting with <c>local[</c>) in the run's order, for a run that pauses.</summary>
    public void CopyLocalsTo(Dictionary<string, ushort> destination)
    {
        if (_full is not null)
        {
            foreach (var (key, value) in _full)
                if (key.StartsWith("local[", StringComparison.Ordinal)) destination.Add(key, value);
            return;
        }
        foreach (var key in _initial.Concat(_appended))
            if (key.StartsWith("local[", StringComparison.Ordinal) && TryGetValue(key, out var value)) destination.Add(key, value);
    }

    /// <summary>The keys among <paramref name="keys"/> the run holds, in the run's order.</summary>
    public IEnumerable<KeyValuePair<string, ushort>> InOrder(IEnumerable<string> keys)
    {
        if (_full is not null)
        {
            var wanted = keys as ICollection<string> ?? keys.ToHashSet(StringComparer.Ordinal);
            return _full.Where(pair => wanted.Contains(pair.Key)).ToList();
        }
        var store = _store!;
        var result = new List<(long Rank, KeyValuePair<string, ushort> Pair)>();
        foreach (var key in keys)
        {
            if (!TryGetValue(key, out var value)) continue;
            long rank = _appendedIndex.TryGetValue(key, out var appended) ? (2L << 40) + appended
                : _initial.IndexOf(key) is >= 0 and var first ? (1L << 40) + first
                : store.SlotOf(key);
            result.Add((rank, new(key, value)));
        }
        return result.OrderBy(item => item.Rank).Select(item => item.Pair).ToList();
    }

    /// <summary>
    /// The variables a <c>print</c> converts its text with (<see cref="ScummPrintRequest.Variables"/>). The print converts its text before the run goes
    /// on, so it reads the run's variables directly instead of a copy of all of them (made for every print until 20 October 2026). After the print
    /// the view is detached: the request outlives the run's variables, and a later read throws instead of reading another run's values.
    /// </summary>
    internal sealed class PrintView(ScummRunVariables run) : IReadOnlyDictionary<string, ushort>
    {
        private ScummRunVariables? _run = run;
        private ScummRunVariables Run => _run ?? throw new InvalidOperationException("A print's variables are read only while the print runs.");
        public void Detach() => _run = null;
        public ushort this[string key] => Run[key];
        public IEnumerable<string> Keys => ((IReadOnlyDictionary<string, ushort>)Run).Keys;
        public IEnumerable<ushort> Values => ((IReadOnlyDictionary<string, ushort>)Run).Values;
        public int Count => Run.Count;
        public bool ContainsKey(string key) => Run.ContainsKey(key);
        public bool TryGetValue(string key, out ushort value) => Run.TryGetValue(key, out value);
        public IEnumerator<KeyValuePair<string, ushort>> GetEnumerator() => Run.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public int Count => _full?.Count ?? Ordered().Count;
    public bool IsReadOnly => false;
    public ICollection<string> Keys => _full?.Keys ?? (ICollection<string>)Ordered().Select(pair => pair.Key).ToList();
    public ICollection<ushort> Values => _full?.Values ?? (ICollection<ushort>)Ordered().Select(pair => pair.Value).ToList();
    IEnumerable<string> IReadOnlyDictionary<string, ushort>.Keys => Keys;
    IEnumerable<ushort> IReadOnlyDictionary<string, ushort>.Values => Values;
    public void Add(KeyValuePair<string, ushort> item) => Add(item.Key, item.Value);
    public bool Contains(KeyValuePair<string, ushort> item) => TryGetValue(item.Key, out var value) && value == item.Value;
    public bool Remove(KeyValuePair<string, ushort> item) => Contains(item) && Remove(item.Key);
    public void CopyTo(KeyValuePair<string, ushort>[] array, int arrayIndex)
    {
        foreach (var pair in this) array[arrayIndex++] = pair;
    }
    public IEnumerator<KeyValuePair<string, ushort>> GetEnumerator() =>
        _full is not null ? _full.GetEnumerator() : Ordered().GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

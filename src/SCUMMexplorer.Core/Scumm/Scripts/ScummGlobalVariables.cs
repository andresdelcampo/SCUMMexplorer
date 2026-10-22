using System.Collections;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// The runtime's global variables (<see cref="ScummScriptRuntimeState.GlobalVariables"/>): a string-keyed dictionary that enumerates exactly as
/// <see cref="Dictionary{TKey,TValue}"/> does (insertion order, a removed entry's slot reused by the next addition, last freed first), and that,
/// while a script run is active, records every write and removal (key, whether it existed, the value before). A run then reads the globals
/// through this store and copies back only what it changed, instead of copying every global at its start and at each event
/// (globals change tracking, 20 October 2026). Callers use it as they used the dictionary.
/// </summary>
public sealed class ScummGlobalVariables : IDictionary<string, ushort>, IReadOnlyDictionary<string, ushort>
{
    internal readonly record struct Change(string Key, bool Existed, ushort Old);

    private readonly Dictionary<string, int> _slots = new(StringComparer.Ordinal);
    private string?[] _keys = new string?[64];
    private ushort[] _values = new ushort[64];
    // When each slot's key was added (a count of additions): a run tells the keys it started with from those added since.
    private long[] _addedAt = new long[64];
    private long _additions;
    private int _used;
    // The free slots, last freed first (Dictionary's free list).
    private readonly Stack<int> _free = new();
    private int _version;
    private int _activeRuns;
    private readonly List<Change> _journal = [];
    private readonly List<ScummRunVariables> _fastRuns = [];

    /// <summary>Keys that start with <c>local[</c> (a run's own keys; a run cannot read the globals through the store while any exists).</summary>
    internal int LocalKeyCount { get; private set; }
    /// <summary>Writes and removals since the outermost active run began; empty when no run is active.</summary>
    internal List<Change> Journal => _journal;

    public int Count => _slots.Count;
    public bool IsReadOnly => false;

    public ushort this[string key]
    {
        get => _slots.TryGetValue(key, out var slot) ? _values[slot] : throw new KeyNotFoundException($"The given key '{key}' was not present in the dictionary.");
        set => Set(key, value);
    }

    public ICollection<string> Keys => Array.AsReadOnly(EnumerateKeys().ToArray());
    public ICollection<ushort> Values => Array.AsReadOnly(this.Select(pair => pair.Value).ToArray());
    IEnumerable<string> IReadOnlyDictionary<string, ushort>.Keys => Keys;
    IEnumerable<ushort> IReadOnlyDictionary<string, ushort>.Values => Values;

    public bool ContainsKey(string key) => _slots.ContainsKey(key);
    public bool TryGetValue(string key, out ushort value)
    {
        if (_slots.TryGetValue(key, out var slot)) { value = _values[slot]; return true; }
        value = 0;
        return false;
    }

    /// <summary>The key's place in the enumeration order (its slot); -1 when absent.</summary>
    internal int SlotOf(string key) => _slots.TryGetValue(key, out var slot) ? slot : -1;
    internal string? KeyAt(int slot) => _keys[slot];
    internal int SlotCount => _used;
    internal ushort ValueAt(int slot) => _values[slot];
    internal long AddedAt(int slot) => _addedAt[slot];
    internal long AddSequence => _additions;

    private void Set(string key, ushort value)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (_slots.TryGetValue(key, out var slot))
        {
            if (_activeRuns > 0) _journal.Add(new(key, true, _values[slot]));
            _values[slot] = value;
            return;
        }
        if (_activeRuns > 0) _journal.Add(new(key, false, 0));
        if (_free.Count > 0) slot = _free.Pop();
        else
        {
            if (_used == _keys.Length)
            {
                Array.Resize(ref _keys, _used * 2);
                Array.Resize(ref _values, _used * 2);
                Array.Resize(ref _addedAt, _used * 2);
            }
            slot = _used++;
        }
        _keys[slot] = key;
        _values[slot] = value;
        _addedAt[slot] = _additions++;
        _slots.Add(key, slot);
        if (key.StartsWith("local[", StringComparison.Ordinal)) LocalKeyCount++;
        _version++;
    }

    public void Add(string key, ushort value)
    {
        if (_slots.ContainsKey(key)) throw new ArgumentException($"An item with the same key has already been added. Key: {key}");
        Set(key, value);
    }

    public bool TryAdd(string key, ushort value)
    {
        if (_slots.ContainsKey(key)) return false;
        Set(key, value);
        return true;
    }

    public bool Remove(string key) => Remove(key, out _);

    public bool Remove(string key, out ushort value)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!_slots.TryGetValue(key, out var slot)) { value = 0; return false; }
        // A run that reads through the store keeps its own copy from here on, made before the removal changes the order.
        MaterializeFastRuns();
        value = _values[slot];
        if (_activeRuns > 0) _journal.Add(new(key, true, value));
        _slots.Remove(key);
        _keys[slot] = null;
        _values[slot] = 0;
        _free.Push(slot);
        if (key.StartsWith("local[", StringComparison.Ordinal)) LocalKeyCount--;
        return true;
    }

    public void Clear()
    {
        MaterializeFastRuns();
        if (_activeRuns > 0)
            foreach (var (key, value) in this) _journal.Add(new(key, true, value));
        _slots.Clear();
        Array.Clear(_keys);
        Array.Clear(_values);
        _used = 0;
        _free.Clear();
        LocalKeyCount = 0;
        _version++;
    }

    private void MaterializeFastRuns()
    {
        for (var index = _fastRuns.Count - 1; index >= 0; index--) _fastRuns[index].Materialize();
    }

    internal void RegisterFastRun(ScummRunVariables run) => _fastRuns.Add(run);
    internal void UnregisterFastRun(ScummRunVariables run) => _fastRuns.Remove(run);

    /// <summary>Marks a script run as active: writes are recorded until the outermost run ends.</summary>
    internal RunScope BeginRun()
    {
        _activeRuns++;
        return new RunScope(this);
    }

    internal sealed class RunScope(ScummGlobalVariables owner) : IDisposable
    {
        private ScummGlobalVariables? _owner = owner;
        public ScummRunVariables? Run { get; set; }
        public void Dispose()
        {
            if (_owner is not { } store) return;
            _owner = null;
            if (Run is not null) store._fastRuns.Remove(Run);
            if (--store._activeRuns == 0)
            {
                store._journal.Clear();
                store._fastRuns.Clear();
            }
        }
    }

    private IEnumerable<string> EnumerateKeys()
    {
        for (var slot = 0; slot < _used; slot++)
            if (_keys[slot] is { } key) yield return key;
    }

    public bool Contains(KeyValuePair<string, ushort> item) => TryGetValue(item.Key, out var value) && value == item.Value;
    public void Add(KeyValuePair<string, ushort> item) => Add(item.Key, item.Value);
    public bool Remove(KeyValuePair<string, ushort> item) => Contains(item) && Remove(item.Key);
    public void CopyTo(KeyValuePair<string, ushort>[] array, int arrayIndex)
    {
        foreach (var pair in this) array[arrayIndex++] = pair;
    }

    public Enumerator GetEnumerator() => new(this);
    IEnumerator<KeyValuePair<string, ushort>> IEnumerable<KeyValuePair<string, ushort>>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Slots in order, free ones skipped; an addition during enumeration throws, as Dictionary's does (a value change or removal does not).</summary>
    public struct Enumerator : IEnumerator<KeyValuePair<string, ushort>>
    {
        private readonly ScummGlobalVariables _owner;
        private readonly int _version;
        private int _slot;
        public Enumerator(ScummGlobalVariables owner) { _owner = owner; _version = owner._version; _slot = -1; Current = default; }
        public KeyValuePair<string, ushort> Current { get; private set; }
        readonly object IEnumerator.Current => Current;
        public bool MoveNext()
        {
            if (_version != _owner._version) throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
            while (++_slot < _owner._used)
                if (_owner._keys[_slot] is { } key) { Current = new(key, _owner._values[_slot]); return true; }
            Current = default;
            return false;
        }
        public void Reset() { _slot = -1; Current = default; }
        public readonly void Dispose() { }
    }
}

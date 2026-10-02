using System.Globalization;

namespace SCUMMexplorer.Core.Formats;

public sealed record LoomRuntimeDependency(string Key, long? LocalSlot);
public sealed record LoomRuntimeRead(long Id, LoomScriptObservation Observation,
    LoomScriptOperandBinding Binding, IReadOnlyList<LoomRuntimeDependency> Dependencies,
    LoomRuntimeWrite? ObservedOrigin, LoomRuntimeRead? IndexRead = null);
public sealed record LoomRuntimeWrite(long Id, LoomScriptObservation Observation,
    LoomScriptOperandBinding Destination, IReadOnlyList<long> SourceReads,
    ushort Value, bool ValueEstimated, long ConfirmationSequence);
public sealed record LoomRuntimeInvalidation(long Read, string Reason);
public sealed record LoomRuntimeRelationUpdate(long Event, LoomScriptObservation? Observation,
    IReadOnlyList<LoomRuntimeRead> Reads, IReadOnlyList<LoomRuntimeWrite> Writes,
    IReadOnlyList<LoomRuntimeInvalidation> Invalidated, string? Barrier = null);

/// <summary>
/// Optional, bounded accounting of copied selected runtime observations. A live relation means
/// no recorded dependency invalidation, never exhaustive access coverage or domain/effect proof.
/// Historical read/write IDs survive invalidation; their correspondence to current storage does
/// not. Word/bit aliases conservatively share a dependency; locals belong to causal slots.
/// NotifyExternal must be used for effects outside the observed stream, including same-value
/// writes. Undetectable external writes and concurrency remain limits of every relation.
/// </summary>
public sealed class LoomRuntimeRelationTracker
{
    private readonly Action<LoomRuntimeRelationUpdate> receive;
    private readonly int relationLimit;
    private readonly Dictionary<long, LoomRuntimeRead> active = [];
    private readonly Dictionary<LoomRuntimeDependency, LoomRuntimeWrite> origins = [];
    private readonly Dictionary<LoomRuntimeDependency, (ushort Value, bool Estimated)> previous = [];
    private readonly Dictionary<long, PendingWrite> pending = [];
    private long ids, events, lastSequence;

    private sealed record PendingWrite(LoomScriptObservation Observation, LoomScriptOperandBinding Destination,
        IReadOnlyList<long> Sources, ushort Expected);

    public LoomRuntimeRelationTracker(Action<LoomRuntimeRelationUpdate> receive, int relationLimit = 4096)
    {
        ArgumentNullException.ThrowIfNull(receive);
        if (relationLimit is < 1 or > 100_000) throw new ArgumentOutOfRangeException(nameof(relationLimit));
        this.receive = receive;
        this.relationLimit = relationLimit;
    }

    public IReadOnlyList<LoomRuntimeRead> ActiveRelations => Array.AsReadOnly(active.Values.ToArray());

    /// <summary>
    /// Invalidate selected global aliases, or all storage when keys are null. This also rejects
    /// pending confirmations and writer candidates, even when the new numeric value is identical.
    /// External/session code is not automatically an exhaustive source-instruction access log.
    /// </summary>
    public void NotifyExternal(IEnumerable<string>? keys = null, string reason = "external or omitted effect")
    {
        var invalidated = new List<LoomRuntimeInvalidation>();
        if (keys is null) Clear(reason, invalidated);
        else foreach (var key in keys)
        {
            // An external local without a causal slot cannot safely name one instance.
            if (key.StartsWith("local[", StringComparison.Ordinal)) Clear(reason, invalidated);
            else Invalidate(Location(key, 0), reason, invalidated);
        }
        receive(new(++events, null, [], [], Frozen(invalidated), reason));
    }

    public void Observe(LoomScriptObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (observation.Sequence <= lastSequence)
            throw new ArgumentException("Runtime observations must retain their original increasing sequence.", nameof(observation));
        lastSequence = observation.Sequence;
        var reads = new List<LoomRuntimeRead>();
        var writes = new List<LoomRuntimeWrite>();
        var invalidated = new List<LoomRuntimeInvalidation>();
        string? barrier = null;
        if (observation.CancelledSlots is { } cancelled)
        {
            foreach (var id in active.Where(p => cancelled.Contains(p.Value.Observation.Slot) ||
                p.Value.Dependencies.Any(d => d.LocalSlot is { } slot && cancelled.Contains(slot)) ||
                p.Value.ObservedOrigin is { } origin && cancelled.Contains(origin.Observation.Slot)).Select(p => p.Key).ToArray())
                Remove(id, "slot cancelled or replaced", invalidated);
            foreach (var key in origins.Where(p => cancelled.Contains(p.Value.Observation.Slot)).Select(p => p.Key).ToArray()) origins.Remove(key);
            foreach (var run in pending.Where(p => cancelled.Contains(p.Value.Observation.Slot)).Select(p => p.Key).ToArray()) pending.Remove(run);
            foreach (var key in previous.Keys.Where(k => k.LocalSlot is { } slot && cancelled.Contains(slot)).ToArray()) previous.Remove(key);
        }
        else
        {
            var confirmed = new HashSet<LoomRuntimeDependency>();
            if (pending.Remove(observation.Invocation, out var candidate) &&
                observation.Slot == candidate.Observation.Slot && observation.ParentSlot == candidate.Observation.ParentSlot &&
                observation.Site is { Stage: "instruction" or "end" } confirmationSite &&
                confirmationSite.SameResource(candidate.Observation.Site) && observation.UnresolvedReason is null &&
                candidate.Destination.Key is { } confirmedKey &&
                observation.Values.TryGetValue(confirmedKey, out var confirmedValue) && confirmedValue == candidate.Expected)
            {
                var write = new LoomRuntimeWrite(++ids, candidate.Observation, candidate.Destination,
                    candidate.Sources, confirmedValue, Estimated(observation, confirmedKey, candidate.Destination.BackingWord), observation.Sequence);
                var location = Location(confirmedKey, observation.Slot);
                origins[location] = write;
                confirmed.Add(location);
                writes.Add(write);
            }
            // Numeric equality alone never preserves a relation across an observed write.
            // Snapshot changes catch additional effects; unchanged unobserved effects require NotifyExternal.
            var present = observation.Values.Keys.Select(k => SnapshotLocation(k, observation.Slot)).ToHashSet();
            foreach (var key in previous.Keys.Where(k => (k.LocalSlot is null || k.LocalSlot == observation.Slot) && !present.Contains(k)).ToArray())
            { Invalidate(Location(key.Key, observation.Slot), "missing or filtered snapshot dependency", invalidated); previous.Remove(key); }
            foreach (var (key, value) in observation.Values)
            {
                var location = Location(key, observation.Slot);
                var snapshot = SnapshotLocation(key, observation.Slot);
                var now = (value, Estimated(observation, key, key == location.Key ? null : location.Key));
                if (previous.TryGetValue(snapshot, out var old) && old != now && !confirmed.Contains(location))
                    Invalidate(location, "changed snapshot or external effect", invalidated);
                previous[snapshot] = now;
            }
            if (observation.Site is { Stage: "instruction" } site)
            {
                var bindings = observation.SourceAccess?.Bindings ?? observation.OperandBindings;
                if (observation.SourceAccess?.Barrier is not null || bindings is null ||
                    observation.SourceAccess is null && bindings.Count == 0 && site.Operation is not
                    ("stopObjectCode" or "breakHere" or "jumpRelative" or "debug"))
                {
                    barrier = observation.SourceAccess?.Barrier ?? "unmodelled instruction access";
                    Clear(barrier, invalidated);
                }
                else
                {
                    var arithmetic = site.Operation is "add" or "subtract" or "multiply" or "divide" or "and" or "or" or "increment" or "decrement";
                    var readCount = bindings.Sum(b => b.Role != "destination-before" || arithmetic
                        ? ReadCount(b) : b.Index is { } index ? ReadCount(index) : 0);
                    if (active.Count + readCount >= relationLimit || origins.Count >= relationLimit ||
                        pending.Count >= relationLimit || previous.Count > relationLimit)
                    { barrier = "relation bound reached"; Clear(barrier, invalidated); }
                    if (previous.Count > relationLimit) previous.Clear();
                    LoomRuntimeRead Read(LoomScriptOperandBinding binding)
                    {
                        var indexRead = binding.Index is { } index ? Read(index) : null;
                        var dependencies = new List<LoomRuntimeDependency>();
                        Dependencies(binding, observation.Slot, dependencies);
                        LoomRuntimeWrite? origin = null;
                        if (binding.Key is { } key && binding.Value is { } number &&
                            origins.TryGetValue(Location(key, observation.Slot), out var found) &&
                            found.Destination.Key == key && found.Value == number) origin = found;
                        var read = new LoomRuntimeRead(++ids, observation, binding,
                            Frozen(dependencies.Distinct()), origin, indexRead);
                        reads.Add(read);
                        if (binding.Value is not null && dependencies.Count > 0) active.Add(read.Id, read);
                        return read;
                    }
                    // A single oversized argument/index batch cannot overrun the storage bound.
                    var retain = readCount <= relationLimit && bindings.Count <= relationLimit;
                    if (!retain) { barrier = "relation bound reached"; Clear(barrier, invalidated); }
                    if (retain)
                        foreach (var binding in bindings.Where(b => b.Role != "destination-before")) Read(binding);
                    foreach (var destination in bindings.Where(b => b.Role == "destination-before"))
                    {
                        if (retain)
                        {
                            if (arithmetic) Read(destination with { Role = "destination-value" });
                            else if (destination.Index is { } index) Read(index);
                        }
                        if (destination.Key is null || destination.Index is { Value: null } ||
                            site.Operation == "setVarRange" && observation.SourceAccess is null)
                        { barrier = "unresolved or ranged write"; Clear(barrier, invalidated); }
                        else Invalidate(Location(destination.Key, observation.Slot), "source write (including same value and aliases)", invalidated);
                    }
                    // Captured numeric operands constrain a candidate only. The next snapshot in
                    // this invocation/resource/causal parent must confirm it; no source hypothesis or effect is inferred.
                    if (retain && bindings.FirstOrDefault(b => b.Role == "destination-before") is { Key: not null } dest &&
                        TryExpected(site.Operation, dest, bindings, out var expected))
                        pending[observation.Invocation] = new(observation, dest, Frozen(reads.Select(r => r.Id)),
                            expected);
                }
            }
            if (observation.Site is { Stage: "end" })
            {
                pending.Remove(observation.Invocation);
                if (observation.UnresolvedReason is not null)
                { barrier = "unresolved execution or omitted effects"; Clear(barrier, invalidated); }
                if (observation.ReachedStop == true && observation.ResumeOffset is null)
                    foreach (var location in previous.Keys.Where(k => k.LocalSlot == observation.Slot).ToArray())
                    { Invalidate(location, "local slot ended", invalidated); previous.Remove(location); }
            }
        }
        receive(new(++events, observation, Frozen(reads), Frozen(writes), Frozen(invalidated), barrier));
    }

    private void Invalidate(LoomRuntimeDependency location, string reason, List<LoomRuntimeInvalidation> output)
    {
        foreach (var id in active.Where(p => p.Value.Dependencies.Contains(location)).Select(p => p.Key).ToArray()) Remove(id, reason, output);
        origins.Remove(location);
        foreach (var run in pending.Where(p => PendingDependsOn(p.Value, location)).Select(p => p.Key).ToArray()) pending.Remove(run);
    }

    private void Clear(string reason, List<LoomRuntimeInvalidation> output)
    {
        foreach (var id in active.Keys.ToArray()) Remove(id, reason, output);
        origins.Clear(); pending.Clear();
    }

    private void Remove(long id, string reason, List<LoomRuntimeInvalidation> output)
    { active.Remove(id); output.Add(new(id, reason)); }

    private static void Dependencies(LoomScriptOperandBinding binding, long slot, List<LoomRuntimeDependency> output)
    {
        if (binding.Key is { } key) output.Add(Location(key, slot));
        if (binding.BackingWord is { } word) output.Add(Location(word, slot));
        if (binding.Index is { } index) Dependencies(index, slot, output);
    }

    private static bool PendingDependsOn(PendingWrite pending, LoomRuntimeDependency location)
    {
        var dependencies = new List<LoomRuntimeDependency>();
        Dependencies(pending.Destination, pending.Observation.Slot, dependencies);
        return dependencies.Contains(location);
    }

    private static int ReadCount(LoomScriptOperandBinding binding) => 1 + (binding.Index is { } index ? ReadCount(index) : 0);

    private static bool TryExpected(string? operation, LoomScriptOperandBinding destination,
        IReadOnlyList<LoomScriptOperandBinding> bindings, out ushort expected)
    {
        expected = 0;
        var right = bindings.FirstOrDefault(b => b.Role == "source")?.Value;
        if (operation == "move" && right is { } number)
        {
            expected = destination.Key!.StartsWith("bit[", StringComparison.Ordinal)
                ? (ushort)(number == 0 ? 0 : 1) : number;
            return true;
        }
        if (destination.Value is not { } left || destination.Key!.StartsWith("bit[", StringComparison.Ordinal)) return false;
        if (operation is not ("increment" or "decrement") && right is null) return false;
        expected = operation switch {
            "add" => unchecked((ushort)(left + right!.Value)),
            "subtract" => unchecked((ushort)(left - right!.Value)),
            "multiply" => unchecked((ushort)(left * right!.Value)),
            "divide" when right != 0 => (ushort)(left / right!.Value),
            "and" => (ushort)(left & right!.Value),
            "or" => (ushort)(left | right!.Value),
            "increment" => unchecked((ushort)(left + 1)),
            "decrement" => unchecked((ushort)(left - 1)),
            _ => 0
        };
        return operation is "add" or "subtract" or "multiply" or "and" or "or" or "increment" or "decrement" ||
            operation == "divide" && right != 0;
    }

    private static LoomRuntimeDependency Location(string key, long slot)
    {
        if (key.StartsWith("bit[0x", StringComparison.Ordinal) &&
            int.TryParse(key.AsSpan(6, key.Length - 7), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var bit))
            key = $"var[0x{bit >> 4:X3}]";
        return new(key, key.StartsWith("local[", StringComparison.Ordinal) ? slot : null);
    }

    private static LoomRuntimeDependency SnapshotLocation(string key, long slot) =>
        new(key, key.StartsWith("local[", StringComparison.Ordinal) ? slot : null);

    private static bool Estimated(LoomScriptObservation observation, string key, string? backing) =>
        observation.Estimated.Contains(key) || backing is not null && observation.Estimated.Contains(backing);
    private static IReadOnlyList<T> Frozen<T>(IEnumerable<T> values) => Array.AsReadOnly(values.ToArray());
}

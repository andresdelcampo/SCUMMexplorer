namespace SCUMMexplorer.Core.Scumm;

public sealed record ScummNativeStorage(string Family, int Subject);
public sealed record ScummNativeRead(long Id, ScummScriptObservation Observation, ScummScriptNativeDependency Dependency,
    ScummNativeStorage? Storage, IReadOnlyList<ScummRuntimeDependency> SelectorDependencies);
public sealed record ScummNativeDependencyUpdate(long Event, ScummScriptObservation? Observation,
    IReadOnlyList<ScummNativeRead> Reads, IReadOnlyList<ScummRuntimeInvalidation> Invalidated, string? Barrier = null);

/// <summary>
/// Independent, optional lifetime accounting for copied selected native fields. Survival means
/// no recorded invalidation, never exhaustive native mutation/effect coverage. Numeric selector
/// aliases/indexes and native fields have separate dependencies. History retains all estimates.
/// Raw same-value mutations require notification; callbacks and incomplete fields stay unresolved.
/// </summary>
public sealed class ScummNativeDependencyTracker
{
    private readonly Action<ScummNativeDependencyUpdate> receive;
    private readonly int relationLimit;
    private readonly Dictionary<long, ScummNativeRead> active = [];
    private long ids, events, lastSequence;

    public ScummNativeDependencyTracker(Action<ScummNativeDependencyUpdate> receive, int relationLimit = 4096)
    {
        ArgumentNullException.ThrowIfNull(receive);
        if (relationLimit is < 1 or > 100_000) throw new ArgumentOutOfRangeException(nameof(relationLimit));
        this.receive = receive;
        this.relationLimit = relationLimit;
    }

    public IReadOnlyList<ScummNativeRead> ActiveRelations => Frozen(active.Values);

    /// <summary>
    /// Report a native write/removal/estimate change, including identical values. Object state and
    /// classes share estimate storage; actor query fields conservatively share one actor lifetime.
    /// Actor palette alone has no captured placement dependency. Unknown domains retire everything.
    /// This API changes no native storage and detects no unnotified mutation.
    /// </summary>
    public void NotifyNativeMutation(string domain, int? subject = null, string reason = "notified native mutation (including same value)")
    {
        ArgumentNullException.ThrowIfNull(domain);
        if (domain == "actor-palette") { Publish(null, [], [], reason); return; }
        var family = Family(domain);
        var invalidated = new List<ScummRuntimeInvalidation>();
        Retire(r => family is null || r.Storage is { } storage && storage.Family == family &&
            (subject is null || storage.Subject == subject), reason, invalidated);
        Publish(null, [], invalidated, reason);
    }

    public void NotifyGlobalMutation(string? key = null, string reason = "notified native selector mutation (including same value)")
    {
        var invalidated = new List<ScummRuntimeInvalidation>();
        Retire(r => key is null || key.StartsWith("local[", StringComparison.Ordinal) ||
            r.SelectorDependencies.Contains(ScummRuntimeRelationTracker.Location(key, 0)), reason, invalidated);
        Publish(null, [], invalidated, reason);
    }

    public void Observe(ScummScriptObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (observation.Sequence <= lastSequence) throw new ArgumentException("Native observations must retain increasing source sequence.", nameof(observation));
        var omitted = lastSequence != 0 && observation.Sequence != lastSequence + 1;
        lastSequence = observation.Sequence;
        var reads = new List<ScummNativeRead>();
        var invalidated = new List<ScummRuntimeInvalidation>();
        string? barrier = null;
        if (omitted)
        {
            // A missing record may contain an identical native/selector mutation or
            // a cancelled scope. Equal later values cannot bridge that missing lifetime.
            // Fresh copied reads below retain their own IDs and independent estimates.
            barrier = "missing native observation sequence boundary";
            Retire(_ => true, barrier, invalidated);
        }
        if (observation.CancelledSlots is { } cancelled)
            Retire(r => cancelled.Contains(r.Observation.Slot) || r.SelectorDependencies.Any(d => d.LocalSlot is { } slot && cancelled.Contains(slot)),
                "native observation slot cancelled or replaced", invalidated);
        else
        {
            Retire(r => !SelectorUnchanged(r, observation), "changed/missing/filtered native selector snapshot", invalidated);
            if (observation.Site is { Stage: "instruction" })
            {
                var access = observation.SourceAccess;
                // Native effects never become numeric lifetime proof. Barriers retire old native
                // correspondence even when a new selected native read can be copied below.
                if (access is null || access.Barrier is not null)
                {
                    barrier = access?.Barrier ?? "unmodelled native access";
                    Retire(_ => true, barrier, invalidated);
                }
                if (access is not null)
                {
                    foreach (var destination in access.Bindings.Where(b => b.Role == "destination-before"))
                    {
                        if (destination.Key is null) Retire(_ => true, "unresolved native selector write", invalidated);
                        else
                        {
                            var key = ScummRuntimeRelationTracker.Location(destination.Key, observation.Slot);
                            Retire(r => r.SelectorDependencies.Contains(key), "native selector source write (including aliases/same value)", invalidated);
                        }
                    }
                    var dependencies = access.NativeDependencies ?? [];
                    if (dependencies.Count > relationLimit || active.Count + dependencies.Count > relationLimit)
                    {
                        barrier = "native relation bound reached";
                        Retire(_ => true, barrier, invalidated);
                    }
                    // Oversized batches are never retained, even after retiring previous reads.
                    if (dependencies.Count <= relationLimit)
                        foreach (var dependency in dependencies)
                        {
                            var parts = Parts(dependency.Selector).Take(33).ToArray();
                            if (parts.Length > 32)
                            {
                                barrier = "native selector index bound reached";
                                Retire(_ => true, barrier, invalidated);
                                continue;
                            }
                            var family = Family(dependency.Domain);
                            var storage = family is not null && dependency.Selector.Value is { } subject
                                ? new ScummNativeStorage(family, family is "message" or "camera" ? 0 : subject) : null;
                            var numeric = new List<ScummRuntimeDependency>();
                            foreach (var part in parts)
                            {
                                if (part.Key is { } key) numeric.Add(ScummRuntimeRelationTracker.Location(key, observation.Slot));
                                if (part.BackingWord is { } word) numeric.Add(ScummRuntimeRelationTracker.Location(word, observation.Slot));
                            }
                            var read = new ScummNativeRead(++ids, observation, dependency, storage, Frozen(numeric.Distinct()));
                            reads.Add(read);
                            // Captured estimates are preserved independently; unresolved native fields
                            // and incomplete/callback selectors cannot establish live correspondence.
                            if (storage is null || dependency.Value is null || dependency.ValueEstimated is null ||
                                dependency.UnresolvedReason is not null || parts.Any(b => b.Value is null || b.UnresolvedReason is not null)) continue;
                            Retire(r => r.Storage == storage && r.Dependency.Domain == dependency.Domain &&
                                (r.Dependency.Value != dependency.Value || r.Dependency.ValueEstimated != dependency.ValueEstimated || r.Dependency.Estimate != dependency.Estimate),
                                "changed native field snapshot", invalidated);
                            active.Add(read.Id, read);
                        }
                }
            }
            if (observation.Site is { Stage: "end" })
            {
                if (observation.UnresolvedReason is not null)
                { barrier = "unresolved native execution/effects"; Retire(_ => true, barrier, invalidated); }
                if (observation.ReachedStop == true && observation.ResumeOffset is null)
                    Retire(r => r.SelectorDependencies.Any(d => d.LocalSlot == observation.Slot), "native selector local slot ended", invalidated);
            }
        }
        Publish(observation, reads, invalidated, barrier);
    }

    private static bool SelectorUnchanged(ScummNativeRead read, ScummScriptObservation observation)
    {
        foreach (var part in Parts(read.Dependency.Selector))
        {
            if (part.Key is not { } key || key.StartsWith("local[", StringComparison.Ordinal) && read.Observation.Slot != observation.Slot) continue;
            var available = observation.Values.TryGetValue(key, out var value);
            // The resolver can read an uncached bit directly from its backing word. A copied
            // snapshot need not contain a separate bit entry; preserve that exact alias read.
            if (!available && part.BackingWord is { } backingKey && observation.Values.TryGetValue(backingKey, out var backingValue) &&
                int.TryParse(key.AsSpan(6, key.Length - 7), System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out var bit))
            { value = (ushort)((backingValue >> (bit & 15)) & 1); available = true; }
            if (!available || part.Value != value ||
                part.ValueEstimated != (observation.Estimated.Contains(key) || part.BackingWord is { } word && observation.Estimated.Contains(word))) return false;
            if (part.BackingWord is { } backing && (!read.Observation.Values.TryGetValue(backing, out var before) ||
                !observation.Values.TryGetValue(backing, out var after) || before != after)) return false;
        }
        return true;
    }

    private static IEnumerable<ScummScriptOperandBinding> Parts(ScummScriptOperandBinding binding)
    {
        for (var current = binding; current is not null; current = current.Index) yield return current;
    }

    private static string? Family(string domain) => domain switch {
        "object-state" or "object-classes" or "object-position" or "object-position-x" or "object-position-y" => "object",
        "user-input" => "user-input",
        "inventory-membership" => "inventory",
        "message" or "message-have-msg" or "message-talk-delay" or "message-talking-actor" => "message",
        "camera" or "camera-destination-x" or "camera-mode" or "camera-follows" or "camera-moving-to-actor" => "camera",
        "actor" or "getActorX" or "getActorY" or "getActorRoom" or "getActorWalkBox" or
        "getActorCostume" or "getActorElevation" or "getActorFacing" or "getActorMoving" => "actor",
        _ => null
    };
    private void Retire(Func<ScummNativeRead, bool> predicate, string reason, List<ScummRuntimeInvalidation> output)
    {
        foreach (var id in active.Where(p => predicate(p.Value)).Select(p => p.Key).ToArray())
        { active.Remove(id); output.Add(new(id, reason)); }
    }
    private void Publish(ScummScriptObservation? observation, IEnumerable<ScummNativeRead> reads,
        IEnumerable<ScummRuntimeInvalidation> invalidated, string? barrier) => receive(new(++events, observation, Frozen(reads), Frozen(invalidated), barrier));
    private static IReadOnlyList<T> Frozen<T>(IEnumerable<T> values) => Array.AsReadOnly(values.ToArray());
}

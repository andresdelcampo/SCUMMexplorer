using System.Collections.ObjectModel;

namespace SCUMMexplorer.Core.Formats;

public sealed record LoomScriptObservationSite(int Script, string? Context, int Container, int ResourceOffset,
    string Stage, int Offset, string? Operation = null, string? Operands = null);

/// <summary>
/// A copied observation of selected explorer execution. Sequence orders instructions within a
/// frame too; Slot persists across resumptions, while Invocation identifies one executor run.
/// Values/estimates and cancellation targets are immutable copies, never runtime references.
/// An end/self-stop/cancellation is not a completion summary or an all-path/effect proof.
/// </summary>
public sealed record LoomScriptObservation(long Sequence, long Slot, long? ParentSlot, long Invocation,
    LoomScriptObservationSite? Site, IReadOnlyDictionary<string, ushort> Values, IReadOnlyList<string> Estimated,
    bool PathEstimated, int? ResumeOffset = null, bool? ReachedStop = null, string? UnresolvedReason = null,
    int? CancelledNumber = null, IReadOnlyList<long>? CancelledSlots = null);

/// <summary>
/// Optional source-observation sink. Give each session its own observer. Filters limit captured
/// sites/keys; omitted values or writers stay unknown. No observations influence playback.
/// The callback receives copies only and should record them without reentering the session.
/// </summary>
public sealed class LoomScriptObserver(Action<LoomScriptObservation> receive,
    Func<LoomScriptObservationSite, bool>? includeSite = null, Func<string, bool>? includeKey = null)
{
    private long sequence;
    private long slots;
    private long invocations;

    internal long NewSlot() => ++slots;
    internal long NewInvocation() => ++invocations;

    internal void Observe(long slot, long? parentSlot, long invocation, LoomScriptDisassembly script,
        string stage, int offset, string? operation, string? operands,
        IReadOnlyDictionary<string, ushort> values, IEnumerable<string> estimated, bool path,
        int? resume = null, bool? stop = null, string? unresolved = null)
    {
        var site = new LoomScriptObservationSite(script.ScriptId, script.Context, script.ContainerFileNumber,
            script.ResourceOffset, stage, offset, operation, operands);
        if (includeSite is not null && !includeSite(site)) return;
        var copy = new ReadOnlyDictionary<string, ushort>(values.Where(p => includeKey?.Invoke(p.Key) != false)
            .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal));
        receive(new(++sequence, slot, parentSlot, invocation, site, copy,
            Array.AsReadOnly(estimated.Order(StringComparer.Ordinal).ToArray()), path, resume, stop, unresolved));
    }

    internal void Cancel(int number, long caller, IEnumerable<long> targets)
    {
        receive(new(++sequence, caller, null, 0, null, new ReadOnlyDictionary<string, ushort>(new Dictionary<string, ushort>()),
            Array.AsReadOnly(Array.Empty<string>()), false, CancelledNumber: number,
            CancelledSlots: Array.AsReadOnly(targets.ToArray())));
    }
}

/// <summary>Identity passed by the session to an observed executor run; no mutable session state.</summary>
public sealed record LoomScriptObservationContext(LoomScriptObserver Observer, long Slot, long? ParentSlot = null);

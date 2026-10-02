namespace SCUMMexplorer.Core.Formats;

/// <summary>A copied actual caller instruction; matching argument numbers prove no transfer.</summary>
public sealed record LoomObservedCall(long Id, long? InstructionSequence, long Slot, long? ParentSlot,
    long Invocation, LoomScriptObservationSite Site, IReadOnlyList<LoomScriptOperandBinding> Operands,
    bool PathEstimated);

/// <summary>
/// Separate actual dispatch/initialization/continuation chronology. Supplied locals are copied
/// from the executor's assignments, never inferred from caller equality or given numeric origins.
/// Dispatch return is a host boundary even when the child yielded, failed, chained or was absent.
/// Ends, caller continuation and resumed locals remain independent of completion and effects.
/// </summary>
public sealed record LoomCallObservation(long Sequence, string Stage, LoomObservedCall? Call,
    long Slot, long? ParentSlot, long Invocation, LoomScriptObservationSite Site,
    IReadOnlyList<LoomScriptOperandBinding> Locals, bool PathEstimated,
    long? InstructionSequence = null, int? ResumeOffset = null, bool? ReachedStop = null,
    string? Barrier = null);

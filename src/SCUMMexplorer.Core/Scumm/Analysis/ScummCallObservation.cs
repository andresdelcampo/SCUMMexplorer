namespace SCUMMexplorer.Core.Scumm;

/// <summary>A copied actual caller instruction; matching argument numbers prove no transfer.</summary>
public sealed record ScummObservedCall(long Id, long? InstructionSequence, long Slot, long? ParentSlot,
    long Invocation, ScummScriptObservationSite Site, IReadOnlyList<ScummScriptOperandBinding> Operands,
    bool PathEstimated);

/// <summary>
/// Separate actual dispatch/initialization/continuation chronology. Supplied locals are copied
/// from the executor's assignments, never inferred from caller equality or given numeric origins.
/// Dispatch return is a host boundary even when the child yielded, failed, chained or was absent.
/// Ends, caller continuation and resumed locals remain independent of completion and effects.
/// </summary>
public sealed record ScummCallObservation(long Sequence, string Stage, ScummObservedCall? Call,
    long Slot, long? ParentSlot, long Invocation, ScummScriptObservationSite Site,
    IReadOnlyList<ScummScriptOperandBinding> Locals, bool PathEstimated,
    long? InstructionSequence = null, int? ResumeOffset = null, bool? ReachedStop = null,
    string? Barrier = null);

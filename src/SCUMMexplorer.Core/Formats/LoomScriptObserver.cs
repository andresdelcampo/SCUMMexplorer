using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace SCUMMexplorer.Core.Formats;

public sealed record LoomScriptObservationSite(int Script, string? Context, int Container, int ResourceOffset,
    string Stage, int Offset, string? Operation = null, string? Operands = null)
{
    internal bool SameResource(LoomScriptObservationSite? other) => other is not null &&
        Script == other.Script && Context == other.Context && Container == other.Container && ResourceOffset == other.ResourceOffset;
}

/// <summary>
/// One decoded operand bound against the copied pre-instruction values. A selected cell and
/// its index retain separate estimates; neither establishes a reached effect or source domain.
/// Destinations describe their value before the instruction, never an inferred write. Missing
/// or filtered values stay unresolved. BackingWord identifies a bit alias, not its writer.
/// </summary>
public sealed record LoomScriptOperandBinding(string Role, string Operand, string? Key, ushort? Value,
    bool? ValueEstimated, LoomScriptOperandBinding? Index = null, string? BackingWord = null,
    int? ArgumentIndex = null, string? UnresolvedReason = null);

/// <summary>
/// Selected numeric storage accesses. A null barrier covers only these operands, never all
/// actor/object/message effects, external writers, scheduling or callee return. Unknown forms
/// are explicit barriers. Legacy OperandBindings keep their original shape and order.
/// </summary>
public sealed record LoomScriptSourceAccess(IReadOnlyList<LoomScriptOperandBinding> Bindings,
    string? Barrier, IReadOnlyList<string> Limits,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<LoomScriptNativeDependency>? NativeDependencies = null);

/// <summary>
/// A copied native query dependency, separate from the numeric selector and inherited path.
/// Value is a selected pre-instruction field, not a query result or a surviving relation.
/// Missing fields/callbacks remain unresolved; no native mutation or effect coverage is inferred.
/// </summary>
public sealed record LoomScriptNativeDependency(string Domain, LoomScriptOperandBinding Selector,
    long? Value, bool? ValueEstimated, string? Estimate = null, string? UnresolvedReason = null);

/// <summary>
/// Actual operand chronology inside one executed expression. InstructionSequence belongs to
/// the unchanged outer stream; Step orders this separate stream. Storage writes and scratch
/// reads are copied at the point they happen, never replayed or turned into numeric origins.
/// Unsupported/partial expressions retain their barrier and all inherited estimates.
/// </summary>
public sealed record LoomExpressionObservation(long InstructionSequence, int Step, long Slot,
    long? ParentSlot, long Invocation, LoomScriptObservationSite Site, string Stage,
    IReadOnlyList<LoomScriptOperandBinding> Bindings, bool PathEstimated, string? Barrier = null);

/// <summary>
/// A copied observation of selected explorer execution. Sequence orders instructions within a
/// frame too; Slot persists across resumptions, while Invocation identifies one executor run.
/// Values/estimates and cancellation targets are immutable copies, never runtime references.
/// An end/self-stop/cancellation is not a completion summary or an all-path/effect proof.
/// </summary>
public sealed record LoomScriptObservation(long Sequence, long Slot, long? ParentSlot, long Invocation,
    LoomScriptObservationSite? Site, IReadOnlyDictionary<string, ushort> Values, IReadOnlyList<string> Estimated,
    bool PathEstimated, int? ResumeOffset = null, bool? ReachedStop = null, string? UnresolvedReason = null,
    int? CancelledNumber = null, IReadOnlyList<long>? CancelledSlots = null,
    IReadOnlyList<LoomScriptOperandBinding>? OperandBindings = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    LoomScriptSourceAccess? SourceAccess = null);

/// <summary>
/// Optional source-observation sink. Give each session its own observer. Filters limit captured
/// sites/keys; omitted values or writers stay unknown. No observations influence playback.
/// The callback receives copies only and should record them without reentering the session.
/// </summary>
public sealed class LoomScriptObserver(Action<LoomScriptObservation> receive,
    Func<LoomScriptObservationSite, bool>? includeSite = null, Func<string, bool>? includeKey = null,
    bool captureOperandBindings = false, LoomRuntimeRelationTracker? runtimeRelations = null,
    bool captureSourceAccess = false, Action<LoomExpressionObservation>? receiveExpression = null,
    LoomNativeDependencyTracker? nativeDependencies = null, Action<LoomCallObservation>? receiveCall = null)
{
    private long sequence;
    private long slots;
    private long invocations;
    private long callSequence, callIds;
    private readonly Dictionary<long, int> callSteps = [];
    private LoomObservedCall? currentCall;
    internal LoomObservedCall? CurrentCall => currentCall;
    internal bool CapturesCalls => receiveCall is not null;
    internal IDisposable? BeginCallRun(long invocation)
    {
        if (receiveCall is null) return null;
        callSteps.Add(invocation, 0);
        return new CallRunScope(this, invocation);
    }
    private sealed class CallRunScope(LoomScriptObserver owner, long invocation) : IDisposable
    {
        public void Dispose() => owner.callSteps.Remove(invocation);
    }

    internal LoomObservedCall? Dispatch(long? instructionSequence, long slot, long? parent, long invocation,
        LoomScriptDisassembly script, LoomScriptInstruction instruction, bool path,
        Func<Func<string, bool>, IReadOnlyList<LoomScriptOperandBinding>> bind)
    {
        if (receiveCall is null || instructionSequence is null || callSteps.GetValueOrDefault(invocation) >= 4096) return null;
        var call = new LoomObservedCall(++callIds, instructionSequence, slot, parent, invocation,
            new(script.ScriptId, script.Context, script.ContainerFileNumber, script.ResourceOffset,
                "instruction", instruction.Offset, instruction.Operation, instruction.Operands),
            Array.AsReadOnly(bind(key => includeKey?.Invoke(key) != false).ToArray()), path);
        CallBoundary("dispatch", call, slot, parent, invocation, call.Site, [], path, instructionSequence);
        return call;
    }

    internal IDisposable EnterDispatch(LoomObservedCall? call) => new DispatchScope(this, call);
    private sealed class DispatchScope : IDisposable
    {
        private readonly LoomScriptObserver owner;
        private readonly LoomObservedCall? previous;
        public DispatchScope(LoomScriptObserver owner, LoomObservedCall? call)
        { this.owner = owner; previous = owner.currentCall; owner.currentCall = call; }
        public void Dispose() => owner.currentCall = previous;
    }

    internal void CallBoundary(string stage, LoomObservedCall? call, long slot, long? parent, long invocation,
        LoomScriptObservationSite site, IReadOnlyList<LoomScriptOperandBinding> locals, bool path,
        long? instructionSequence = null, int? resume = null, bool? stop = null, string? barrier = null)
    {
        if (receiveCall is null) return;
        var step = callSteps.GetValueOrDefault(invocation) + 1;
        callSteps[invocation] = step;
        if (step > 4096)
        {
            if (step == 4097) receiveCall(new(++callSequence, "barrier", null, slot, parent, invocation,
                site with { Operation = null, Operands = null }, [], path, Barrier: "call observation boundary bound reached"));
            return;
        }
        if (includeSite?.Invoke(site) == false)
        {
            // Omitted boundaries never silently bridge this separate stream. Rejected
            // operands and locals are not exposed through the barrier.
            receiveCall(new(++callSequence, "barrier", null, slot, parent, invocation,
                site with { Operation = null, Operands = null }, [], path, Barrier: "filtered call boundary"));
            return;
        }
        receiveCall(new(++callSequence, stage, call, slot, parent, invocation, site,
            Array.AsReadOnly(locals.ToArray()), path, instructionSequence, resume, stop, barrier));
    }

    internal long NewSlot() => ++slots;
    internal long NewInvocation() => ++invocations;

    internal bool CapturesExpressions => receiveExpression is not null;
    internal bool IncludesKey(string key) => includeKey?.Invoke(key) != false;

    internal void Expression(long sequence, int step, long slot, long? parentSlot, long invocation,
        LoomScriptDisassembly script, LoomScriptInstruction instruction, string stage, bool path,
        Func<Func<string, bool>, IReadOnlyList<LoomScriptOperandBinding>> bind, string? barrier = null)
    {
        receiveExpression?.Invoke(new(sequence, step, slot, parentSlot, invocation,
            new(script.ScriptId, script.Context, script.ContainerFileNumber, script.ResourceOffset,
                "instruction", instruction.Offset, instruction.Operation, instruction.Operands), stage,
            Array.AsReadOnly(bind(key => includeKey?.Invoke(key) != false).ToArray()), path, barrier));
    }

    internal long? Observe(long slot, long? parentSlot, long invocation, LoomScriptDisassembly script,
        string stage, int offset, string? operation, string? operands,
        IReadOnlyDictionary<string, ushort> values, IEnumerable<string> estimated, bool path,
        int? resume = null, bool? stop = null, string? unresolved = null,
        Func<Func<string, bool>, IReadOnlyList<LoomScriptOperandBinding>>? bindOperands = null,
        Func<Func<string, bool>, LoomScriptSourceAccess>? bindSourceAccess = null)
    {
        var site = new LoomScriptObservationSite(script.ScriptId, script.Context, script.ContainerFileNumber,
            script.ResourceOffset, stage, offset, operation, operands);
        if (includeSite is not null && !includeSite(site))
        {
            runtimeRelations?.NotifyExternal(reason: "filtered source site");
            nativeDependencies?.NotifyGlobalMutation(reason: "filtered source site");
            return null;
        }
        var copy = new ReadOnlyDictionary<string, ushort>(values.Where(p => includeKey?.Invoke(p.Key) != false)
            .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal));
        var observation = new LoomScriptObservation(++sequence, slot, parentSlot, invocation, site, copy,
            Array.AsReadOnly(estimated.Order(StringComparer.Ordinal).ToArray()), path, resume, stop, unresolved,
            OperandBindings: (captureOperandBindings || runtimeRelations is not null) && bindOperands is not null
                ? Array.AsReadOnly(bindOperands(key => includeKey?.Invoke(key) != false).ToArray()) : null,
            SourceAccess: (captureSourceAccess || runtimeRelations is not null || nativeDependencies is not null) && bindSourceAccess is not null
                ? bindSourceAccess(key => includeKey?.Invoke(key) != false) : null);
        runtimeRelations?.Observe(observation);
        nativeDependencies?.Observe(observation);
        receive(observation);
        return observation.Sequence;
    }

    internal void GlobalMutation(string key)
    {
        runtimeRelations?.NotifyExternal([key], reason: "runtime global mutation (including same value)");
        nativeDependencies?.NotifyGlobalMutation(key);
    }

    internal void NativeMutation(string domain, int? subject) => nativeDependencies?.NotifyNativeMutation(domain, subject);

    internal void Cancel(int number, long caller, IEnumerable<long> targets)
    {
        var observation = new LoomScriptObservation(++sequence, caller, null, 0, null, new ReadOnlyDictionary<string, ushort>(new Dictionary<string, ushort>()),
            Array.AsReadOnly(Array.Empty<string>()), false, CancelledNumber: number,
            CancelledSlots: Array.AsReadOnly(targets.ToArray()));
        runtimeRelations?.Observe(observation);
        nativeDependencies?.Observe(observation);
        receive(observation);
    }
}

/// <summary>Identity passed by the session to an observed executor run; no mutable session state.</summary>
public sealed record LoomScriptObservationContext(LoomScriptObserver Observer, long Slot, long? ParentSlot = null,
    LoomObservedCall? Call = null, bool Resuming = false);

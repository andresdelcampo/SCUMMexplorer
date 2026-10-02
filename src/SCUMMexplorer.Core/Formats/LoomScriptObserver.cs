using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace SCUMMexplorer.Core.Formats;

public sealed record LoomScriptObservationSite(int Script, string? Context, int Container, int ResourceOffset,
    string Stage, int Offset, string? Operation = null, string? Operands = null);

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
    string? Barrier, IReadOnlyList<string> Limits);

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
    bool captureSourceAccess = false)
{
    private long sequence;
    private long slots;
    private long invocations;

    internal long NewSlot() => ++slots;
    internal long NewInvocation() => ++invocations;

    internal void Observe(long slot, long? parentSlot, long invocation, LoomScriptDisassembly script,
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
            return;
        }
        var copy = new ReadOnlyDictionary<string, ushort>(values.Where(p => includeKey?.Invoke(p.Key) != false)
            .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal));
        var observation = new LoomScriptObservation(++sequence, slot, parentSlot, invocation, site, copy,
            Array.AsReadOnly(estimated.Order(StringComparer.Ordinal).ToArray()), path, resume, stop, unresolved,
            OperandBindings: (captureOperandBindings || runtimeRelations is not null) && bindOperands is not null
                ? Array.AsReadOnly(bindOperands(key => includeKey?.Invoke(key) != false).ToArray()) : null,
            SourceAccess: (captureSourceAccess || runtimeRelations is not null) && bindSourceAccess is not null
                ? bindSourceAccess(key => includeKey?.Invoke(key) != false) : null);
        runtimeRelations?.Observe(observation);
        receive(observation);
    }

    internal void GlobalMutation(string key) => runtimeRelations?.NotifyExternal([key],
        reason: "runtime global mutation (including same value)");

    internal void Cancel(int number, long caller, IEnumerable<long> targets)
    {
        var observation = new LoomScriptObservation(++sequence, caller, null, 0, null, new ReadOnlyDictionary<string, ushort>(new Dictionary<string, ushort>()),
            Array.AsReadOnly(Array.Empty<string>()), false, CancelledNumber: number,
            CancelledSlots: Array.AsReadOnly(targets.ToArray()));
        runtimeRelations?.Observe(observation);
        receive(observation);
    }
}

/// <summary>Identity passed by the session to an observed executor run; no mutable session state.</summary>
public sealed record LoomScriptObservationContext(LoomScriptObserver Observer, long Slot, long? ParentSlot = null);

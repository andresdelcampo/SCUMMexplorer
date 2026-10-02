namespace SCUMMexplorer.Core.Formats;

/// <summary>The actual decoded object entry; no inventory, lifetime, effects or return proof.</summary>
public sealed record LoomObjectCalleeSelection(LoomObservedCall Dispatch, int ObjectId, int RequestedVerb,
    LoomObjectVerbEntry Entry, LoomScriptObservationSite Site);

/// <summary>Actual supplied assignment attached to its dispatch; never a numeric origin.</summary>
public sealed record LoomCalleeAssignment(LoomScriptOperandBinding Argument, LoomScriptOperandBinding Local);
public sealed record LoomActualCalleeBinding(LoomObservedCall Dispatch, LoomCallObservation Initialization,
    IReadOnlyList<LoomCalleeAssignment> SuppliedAssignments,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    LoomObjectCalleeSelection? ObjectSelection = null);
public sealed record LoomCallerResumption(LoomCallObservation Yield, LoomCallObservation RestoredLocals,
    LoomScriptObservation FirstInstruction);
/// <summary>Three observed immediate boundaries, not successful return or effects/completion proof.</summary>
public sealed record LoomImmediateStopCorrespondence(LoomCallObservation ChildEnd,
    LoomCallObservation HostReturn, LoomCallObservation CallerContinuation);
public sealed record LoomCallCorrespondence(long Event, LoomObservedCall? Dispatch,
    LoomActualCalleeBinding? Binding, IReadOnlyList<LoomCallObservation> ChildBoundaries,
    LoomCallObservation? HostReturn, LoomCallObservation? CallerContinuation,
    LoomCallObservation? CallerEnd, LoomCallerResumption? CallerResumption,
    bool ChildTerminated, LoomImmediateStopCorrespondence? ImmediateStopCorrespondence,
    string? ChildRejection, string? CallerRejection, string? Barrier, IReadOnlyList<string> Limits);

/// <summary>
/// Optional structural accounting over the actual call and instruction streams. Initialization,
/// child end/yield, host return, caller continuation and restored locals remain independent.
/// Only a complete observed child yield/restoration/end chain may describe later termination.
/// No record enters numeric/native lineage, clears estimates or influences execution. Historical
/// assignment copies survive in published updates; argument equality establishes nothing alone.
/// </summary>
public sealed class LoomCallCorrespondenceTracker
{
    private readonly Action<LoomCallCorrespondence> receive;
    private readonly int callLimit, boundaryLimit;
    private readonly Dictionary<long, Entry> active = [];
    private LoomScriptObservation? lastScript;
    private long lastCallSequence, lastScriptSequence, lastCallId, events;
    private static readonly IReadOnlyList<string> limits = Frozen(new[] {
        "actual assignment and structural boundaries only; no callee-local numeric origins or domain/effect transport",
        "host return, child termination, caller continuation and restored locals are independent",
        "selected streams only; omitted/native/external/concurrent/scheduler/cancellation/replacement limits remain",
        "all argument/local/index/path estimates and historical origins retained; no completion promotion"
    });
    private sealed class Chain(LoomCallObservation start)
    {
        public LoomCallObservation Start { get; } = start;
        public long Run { get; set; } = start.Invocation;
        public HashSet<long> Runs { get; } = [start.Invocation];
        public LoomCallObservation? Yield { get; set; }
        public LoomCallObservation? Restoration { get; set; }
        public LoomScriptObservation? LastInstruction { get; set; }
        public bool AwaitingFirst { get; set; }
        public LoomCallObservation? Terminal { get; set; }
    }
    private sealed class Entry(LoomCallObservation dispatch)
    {
        public LoomCallObservation Dispatch { get; } = dispatch;
        public LoomActualCalleeBinding? Binding { get; set; }
        public LoomObjectCalleeSelection? ObjectSelection { get; set; }
        public Chain? Child { get; set; }
        public Chain Caller { get; } = new(dispatch);
        public LoomObservedCall? CallerIncoming { get; init; }
        public List<LoomCallObservation> ChildBoundaries { get; } = [];
        public LoomCallObservation? HostReturn { get; set; }
        public LoomCallObservation? Continued { get; set; }
        public LoomCallObservation? CallerEnd { get; set; }
        public LoomCallerResumption? Resumption { get; set; }
        public string? ChildRejection { get; set; }
        public string? CallerRejection { get; set; }
        public int Boundaries { get; set; } = 1;
    }

    public LoomCallCorrespondenceTracker(Action<LoomCallCorrespondence> receive, int callLimit = 4096, int boundaryLimit = 128)
    {
        ArgumentNullException.ThrowIfNull(receive);
        if (callLimit is < 1 or > 100_000) throw new ArgumentOutOfRangeException(nameof(callLimit));
        if (boundaryLimit is < 1 or > 4096) throw new ArgumentOutOfRangeException(nameof(boundaryLimit));
        this.receive = receive;
        this.callLimit = callLimit;
        this.boundaryLimit = boundaryLimit;
    }

    public int PendingCalls => active.Count;

    /// <summary>Consume the actual decoder selection before initialization; never infer from a label.</summary>
    public void ObserveObjectSelection(LoomObjectCalleeSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (!active.TryGetValue(selection.Dispatch.Id, out var entry)) return;
        if (!Count(entry)) return;
        var call = entry.Dispatch.Call!;
        var target = call.Operands.FirstOrDefault(b => b.Role == "target");
        var verb = call.Operands.FirstOrDefault(b => b.Role == "verb");
        var selected = selection.Entry;
        if (!SameCall(call, selection.Dispatch) || call.Site.Operation != "startObject" || entry.ObjectSelection is not null ||
            entry.Child is not null || entry.ChildRejection is not null || entry.HostReturn is not null || !Aligned(entry.Dispatch) ||
            call.Operands.Count(b => b.Role == "target") != 1 || call.Operands.Count(b => b.Role == "verb") != 1 ||
            target is not { Value: > 0, UnresolvedReason: null } || target.Value != selection.ObjectId ||
            verb is not { Value: <= 255, UnresolvedReason: null } || verb.Value != selection.RequestedVerb ||
            selected.ObjectId != selection.ObjectId || selected.VerbId != selection.RequestedVerb && !selected.IsFallback ||
            selected.CodeOffset < 0 || selected.TableOffset < selected.CodeOffset + 17 ||
            selected.ScriptOffset < selected.TableOffset + 4 || selected.ScriptOffset >= selected.ResourceEnd ||
            selection.Site is not { Stage: "begin", Offset: 0, Context: not null } ||
            selection.Site.ResourceOffset != selected.ScriptOffset)
        { Retire(entry, "missing/foreign/repeated actual object selection"); return; }
        entry.ObjectSelection = selection;
        Publish(entry);
    }

    /// <summary>Explicitly report an omitted/external boundary; unchanged numbers cannot bridge it.</summary>
    public void NotifyExternal(string reason = "external or omitted call boundary", long? slot = null)
    {
        foreach (var entry in active.Values.Where(e => slot is null || Related(e, slot.Value)).ToArray())
            Retire(entry, reason);
        lastScript = null;
    }

    public void ObserveScript(LoomScriptObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (observation.Sequence <= lastScriptSequence) throw new ArgumentException("Increasing instruction sequence required.", nameof(observation));
        if (lastScriptSequence != 0 && observation.Sequence != lastScriptSequence + 1) NotifyExternal("missing instruction sequence boundary");
        lastScriptSequence = observation.Sequence;
        lastScript = observation;
        if (observation.CancelledSlots is { } cancelled)
        {
            foreach (var entry in active.Values.Where(e => cancelled.Any(s => Related(e, s))).ToArray())
                Retire(entry, "call slot cancelled or replaced");
            return;
        }
        if (observation.Site is not { Stage: "instruction" }) return;
        foreach (var entry in active.Values.ToArray())
        {
            AdvanceInstruction(entry, entry.Child, observation, child: true);
            AdvanceInstruction(entry, entry.Caller, observation, child: false);
        }
    }

    public void ObserveCall(LoomCallObservation boundary)
    {
        ArgumentNullException.ThrowIfNull(boundary);
        if (boundary.Sequence <= lastCallSequence) throw new ArgumentException("Increasing call sequence required.", nameof(boundary));
        if (lastCallSequence != 0 && boundary.Sequence != lastCallSequence + 1) NotifyExternal("missing call sequence boundary");
        lastCallSequence = boundary.Sequence;
        if (boundary.Stage == "barrier")
        {
            // A filtered initialization may precede discovery of the child slot. Its parent
            // identifies pending dispatches too; unrelated scopes do not donate boundaries.
            foreach (var entry in active.Values.Where(e => Related(e, boundary.Slot) ||
                boundary.ParentSlot == e.Dispatch.Slot).ToArray()) Retire(entry, boundary.Barrier ?? "call stream barrier");
            return;
        }
        if (boundary.Stage == "dispatch" && boundary.Call is { } call)
        {
            if (call.Id <= lastCallId) { NotifyExternal("reused call identity"); return; }
            lastCallId = call.Id;
            if (active.Count >= callLimit) NotifyExternal("call correspondence storage bound reached");
            if (!Aligned(boundary) || boundary.Slot != call.Slot || boundary.Invocation != call.Invocation ||
                boundary.ParentSlot != call.ParentSlot || boundary.Site != call.Site)
            { Publish(null, "dispatch has no aligned actual instruction"); return; }
            // The caller's incoming identity must come from its actual, still observed
            // initialization/continuity. Equal restored locals or a matching slot alone
            // cannot supply an omitted incoming dispatch later.
            var incoming = active.Values.FirstOrDefault(e => e.ChildRejection is null && e.Binding is not null &&
                e.Child is { Yield: null, AwaitingFirst: false, Terminal: null } child &&
                child.Start.Slot == boundary.Slot && child.Run == boundary.Invocation &&
                child.Start.ParentSlot == boundary.ParentSlot && child.Start.Site.SameResource(boundary.Site));
            var entry = new Entry(boundary) { CallerIncoming = incoming?.Dispatch.Call };
            entry.Caller.LastInstruction = lastScript;
            active.Add(call.Id, entry);
            Publish(entry);
            return;
        }
        // Caller restoration names the caller's incoming dispatch, not the pending outgoing
        // call. Match its stable slot/resource/parent and exact yielded resume offset instead.
        if (boundary.Stage == "resumed-locals")
            foreach (var pending in active.Values.Where(e => e.Dispatch.Slot == boundary.Slot && e.Caller.Yield is not null).ToArray())
            {
                if (!Count(pending)) continue;
                if (!ObservedCallerIncoming(pending, boundary) || !Restore(pending.Caller, boundary))
                    pending.CallerRejection = "missing/foreign/reused caller restoration";
                Publish(pending);
            }
        if (boundary.Call is not { } observed || !active.TryGetValue(observed.Id, out var current)) return;
        if (!SameCall(current.Dispatch.Call!, observed)) { Retire(current, "foreign dispatch identity"); return; }
        if (!Count(current)) return;
        switch (boundary.Stage)
        {
            case "initialized":
                if (current.Child is not null || current.ChildRejection is not null || !InitializationPhase(current, boundary) ||
                    boundary.ParentSlot != observed.Slot || boundary.Slot == observed.Slot || boundary.Invocation == observed.Invocation ||
                    !Aligned(boundary) || boundary.Barrier is not null || !SelectedCallee(current, boundary) ||
                    !CompleteLocals(boundary, restored: false))
                { current.ChildRejection = "no complete actual callee initialization"; break; }
                var arguments = observed.Operands.Where(b => b.Role == "argument").ToArray();
                if (arguments.Length > 26 || boundary.Locals.Count(l => l.Role == "supplied-local") != arguments.Length || arguments.Where((a, i) => a.ArgumentIndex != i || a.Value is null ||
                    a.UnresolvedReason is not null || boundary.Locals[i].Role != "supplied-local").Any())
                { current.ChildRejection = "unresolved actual supplied assignment"; break; }
                current.Binding = new(observed, boundary, Frozen(arguments.Select((a, i) => new LoomCalleeAssignment(a, boundary.Locals[i]))), current.ObjectSelection);
                current.Child = new(boundary);
                current.ChildBoundaries.Add(boundary);
                break;
            case "resumed-locals":
                if (current.ChildRejection is not null) break;
                if (current.Child is null || !Restore(current.Child, boundary)) current.ChildRejection = "missing/foreign/reused child restoration";
                else current.ChildBoundaries.Add(boundary);
                break;
            case "end":
                if (current.ChildRejection is not null) break;
                if (current.Child is null || !End(current.Child, boundary)) current.ChildRejection = "child end is partial/foreign/omitted/chained";
                else current.ChildBoundaries.Add(boundary);
                break;
            case "dispatch-ended":
                if (!CallerIdentity(current, boundary) || boundary.Site != observed.Site ||
                    boundary.InstructionSequence != observed.InstructionSequence || boundary.PathEstimated != observed.PathEstimated ||
                    boundary.ResumeOffset is not null || boundary.ReachedStop is not null || current.HostReturn is not null)
                    current.CallerRejection = "foreign or repeated host return";
                else current.HostReturn = boundary; // Its barrier describes the host, never child success.
                break;
            case "caller-continued":
                if (boundary.Site.Stage != "instruction" || !CallerIdentity(current, boundary) || !Aligned(boundary) || current.HostReturn is null ||
                    current.Continued is not null || current.CallerEnd is not null || observed.Site.Operation == "chainScript")
                    current.CallerRejection = "no aligned surviving immediate caller continuation";
                else current.Continued = boundary;
                break;
            case "caller-ended":
                if (boundary.Site.Stage != "end" || !CallerIdentity(current, boundary) || !Aligned(boundary) || current.HostReturn is null ||
                    current.Continued is not null || current.CallerEnd is not null)
                    current.CallerRejection = "foreign or repeated caller end";
                else
                {
                    current.CallerEnd = boundary;
                    if (boundary.Barrier is not null) current.CallerRejection = "partial caller end";
                    else if (boundary.ReachedStop == false && boundary.ResumeOffset is not null) current.Caller.Yield = boundary;
                    else if (boundary.ReachedStop != true || boundary.ResumeOffset is not null) current.CallerRejection = "unresolved caller boundary";
                }
                break;
            default: Retire(current, "unsupported call boundary"); return;
        }
        Publish(current);
        // Fully settled histories remain in immutable updates, not unbounded live storage.
        if (current.HostReturn is not null && (current.Continued is not null || current.Resumption is not null ||
            current.CallerEnd is { ReachedStop: true } || current.CallerRejection is not null) &&
            (current.Child?.Terminal is not null || current.ChildRejection is not null ||
             current.Child is null && observed.Site.Operation != "chainScript")) active.Remove(observed.Id);
    }

    private void AdvanceInstruction(Entry entry, Chain? chain, LoomScriptObservation instruction, bool child)
    {
        if (!active.ContainsKey(entry.Dispatch.Call!.Id) || !child && (entry.Continued is not null ||
            entry.Resumption is not null || entry.CallerEnd is { ReachedStop: true } || entry.CallerRejection is not null)) return;
        if (chain is null || chain.Start.Slot != instruction.Slot) return;
        if (!instruction.Site!.SameResource(chain.Start.Site) || instruction.ParentSlot != chain.Start.ParentSlot ||
            chain.Run != instruction.Invocation || chain.Terminal is not null || chain.Yield is not null && !chain.AwaitingFirst)
        { Retire(entry, "instruction breaks call continuity"); return; }
        if (chain.AwaitingFirst)
        {
            if (chain.Restoration is null || instruction.Site.Offset != chain.Restoration.Site.Offset)
            { Retire(entry, "resumed first instruction offset mismatch"); return; }
            chain.AwaitingFirst = false;
            if (!child && entry.CallerRejection is null)
            {
                entry.Resumption = new(chain.Yield!, chain.Restoration, instruction);
                Publish(entry);
                if (entry.Child?.Terminal is not null || entry.ChildRejection is not null) active.Remove(entry.Dispatch.Call!.Id);
            }
            chain.Yield = null;
        }
        chain.LastInstruction = instruction;
    }

    private bool Restore(Chain chain, LoomCallObservation boundary)
    {
        if (boundary.Site.Stage != "begin" || !Aligned(boundary) || boundary.Barrier is not null || !CompleteLocals(boundary, restored: true) ||
            !ChainIdentity(chain, boundary) || chain.Terminal is not null || chain.Yield?.ResumeOffset != boundary.Site.Offset ||
            chain.Yield is null || chain.AwaitingFirst || !chain.Runs.Add(boundary.Invocation)) return false;
        chain.Run = boundary.Invocation;
        chain.Restoration = boundary;
        chain.AwaitingFirst = true;
        chain.LastInstruction = null;
        return true;
    }

    private bool ObservedCallerIncoming(Entry entry, LoomCallObservation boundary) =>
        entry.CallerIncoming is { } incoming && boundary.Call is { } restored && SameCall(incoming, restored) &&
        active.TryGetValue(incoming.Id, out var source) && source.ChildRejection is null && source.Binding is not null &&
        source.Child is { } child && ChainIdentity(child, boundary) && child.Yield is not null &&
        child.Yield.ResumeOffset == boundary.Site.Offset && !child.AwaitingFirst && child.Terminal is null;

    private bool End(Chain chain, LoomCallObservation boundary)
    {
        if (boundary.Site.Stage != "end" || !Aligned(boundary) || !ChainIdentity(chain, boundary) || chain.Run != boundary.Invocation ||
            chain.Terminal is not null || chain.Yield is not null || chain.AwaitingFirst || boundary.Barrier is not null ||
            chain.LastInstruction is null) return false;
        if (boundary.ReachedStop == false && boundary.ResumeOffset is not null) { chain.Yield = boundary; return true; }
        if (boundary.ReachedStop != true || boundary.ResumeOffset is not null || chain.LastInstruction.Site?.Operation != "stopObjectCode") return false;
        chain.Terminal = boundary;
        return true;
    }

    private bool Aligned(LoomCallObservation boundary) => lastScript is { } script && boundary.InstructionSequence == script.Sequence &&
        boundary.Slot == script.Slot && boundary.ParentSlot == script.ParentSlot && boundary.Invocation == script.Invocation &&
        boundary.Site == script.Site && boundary.PathEstimated == script.PathEstimated &&
        boundary.ResumeOffset == script.ResumeOffset && boundary.ReachedStop == script.ReachedStop;
    private static bool InitializationPhase(Entry entry, LoomCallObservation boundary)
    {
        if (boundary.Site is not { Stage: "begin", Offset: 0 } || boundary.ResumeOffset is not null ||
            boundary.ReachedStop is not null) return false;
        // Ordinary calls initialize inside dispatch. A chain is dispatched only after its
        // chaining caller ends; require those actual boundaries instead of treating a late
        // initialization as an ordinary nested call. An opaque caller-end effect remains a
        // caller rejection, independent of the child's actual assignment. Neither phase
        // proves a successful return.
        return entry.Dispatch.Call!.Site.Operation == "chainScript"
            ? entry.HostReturn is not null && entry.CallerRejection is null or "partial caller end" &&
                entry.CallerEnd is { ReachedStop: true, ResumeOffset: null }
            : entry.HostReturn is null && entry.Continued is null && entry.CallerEnd is null;
    }
    private static bool SelectedCallee(Entry entry, LoomCallObservation boundary)
    {
        var call = entry.Dispatch.Call!;
        if (call.Site.Operation == "startObject")
            return entry.ObjectSelection is { } selection && boundary.Site == selection.Site;
        return call.Site.Operation is "startScript" or "chainScript" &&
            call.Operands.FirstOrDefault(b => b.Role == "target") is { Value: { } target, UnresolvedReason: null } &&
            boundary.Site.Script == target;
    }
    private static bool ChainIdentity(Chain chain, LoomCallObservation b) => b.Slot == chain.Start.Slot &&
        b.ParentSlot == chain.Start.ParentSlot && b.Site.SameResource(chain.Start.Site);
    private static bool CallerIdentity(Entry entry, LoomCallObservation b) => b.Slot == entry.Dispatch.Slot &&
        b.ParentSlot == entry.Dispatch.ParentSlot && b.Invocation == entry.Dispatch.Invocation && b.Site.SameResource(entry.Dispatch.Site);
    private static bool CompleteLocals(LoomCallObservation b, bool restored) => b.Locals.Count == 26 &&
        b.Locals.Where((l, i) => l.Key != $"local[0x{i:X3}]" || l.ArgumentIndex != i || l.Value is null ||
            l.ValueEstimated is null || l.UnresolvedReason is not null || (restored ? l.Role != "restored-local" :
                l.Role is not ("supplied-local" or "default-zero"))).Any() == false;
    private static bool SameCall(LoomObservedCall a, LoomObservedCall b) => a.Id == b.Id && a.InstructionSequence == b.InstructionSequence &&
        a.Slot == b.Slot && a.ParentSlot == b.ParentSlot && a.Invocation == b.Invocation && a.Site == b.Site &&
        a.PathEstimated == b.PathEstimated && a.Operands.SequenceEqual(b.Operands);
    private static bool Related(Entry e, long slot) => e.Dispatch.Slot == slot || e.Child?.Start.Slot == slot;
    private bool Count(Entry e)
    {
        if (++e.Boundaries <= boundaryLimit) return true;
        Retire(e, "call correspondence boundary bound reached");
        return false;
    }
    private void Retire(Entry entry, string reason)
    {
        entry.ChildRejection = entry.CallerRejection = reason;
        Publish(entry, reason);
        active.Remove(entry.Dispatch.Call!.Id);
    }
    private void Publish(Entry? entry, string? barrier = null)
    {
        var terminal = entry?.ChildRejection is null ? entry?.Child?.Terminal : null;
        LoomImmediateStopCorrespondence? immediate = null;
        if (terminal is not null && entry!.CallerRejection is null && entry.HostReturn is { } host && entry.Continued is { } caller &&
            terminal.Invocation == entry.Binding!.Initialization.Invocation && terminal.Sequence < host.Sequence && host.Sequence < caller.Sequence)
            immediate = new(terminal, host, caller);
        receive(new(++events, entry?.Dispatch.Call, entry?.Binding, Frozen(entry?.ChildBoundaries ?? []), entry?.HostReturn,
            entry?.Continued, entry?.CallerEnd, entry?.CallerRejection is null ? entry?.Resumption : null,
            terminal is not null, immediate, entry?.ChildRejection, entry?.CallerRejection, barrier, limits));
    }
    private static IReadOnlyList<T> Frozen<T>(IEnumerable<T> values) => Array.AsReadOnly(values.ToArray());
}

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// A selected zero-read and its last surviving snapshot-confirmed literal writer, if any.
/// Matching the causal child is evidence about this stream, never protocol completion.
/// Writer/read value, index, inherited path and terminal observations remain independent.
/// Termination requires the writer's own end, or a separately copied bounded chain of actual
/// yielded ends and matching resumed begins/ends. A later invocation alone proves nothing.
/// </summary>
public sealed record ScummCompletionRead(ScummRuntimeRead Read, ScummRuntimeWrite? Writer,
    bool MatchesOwnLiteralChild, ScummScriptObservation? WriterEnd, string? Rejection,
    IReadOnlyList<string> Limits, bool WriterTerminated = false, string? TerminalRejection = null,
    ScummWriterTerminalContinuity? ResumedTerminalContinuity = null);

/// <summary>Observed writer-to-terminal boundaries only, with every boundary's original estimates.</summary>
public sealed record ScummWriterTerminalContinuity(long Writer, IReadOnlyList<ScummScriptObservation> Boundaries);

/// <summary>
/// Generic zero-wait observation over relation updates, without script IDs, room exceptions,
/// helper summaries or mutable runtime access. Connect Observe to a relation tracker's sink.
/// Filtered/unknown/external writes and cancellations have already retired live writer origins.
/// No observation influences playback, clears estimates or establishes scheduler/callee proof.
/// </summary>
public sealed class ScummCompletionObserver
{
    private readonly Action<ScummCompletionRead> receive;
    private readonly int slotLimit;
    private readonly int boundaryLimit;
    private readonly Dictionary<long, ScummScriptObservation> ends = [];
    private readonly Dictionary<long, Continuity> continuities = [];
    private sealed class Continuity(ScummRuntimeWrite writer)
    {
        public ScummRuntimeWrite Writer { get; } = writer;
        public long Invocation { get; set; } = writer.Observation.Invocation;
        public HashSet<long> Invocations { get; } = [writer.Observation.Invocation];
        public int? ExpectedResume { get; set; }
        public ScummScriptObservation? Terminal { get; set; }
        public List<ScummScriptObservation> Boundaries { get; } = [];
    }
    private static readonly IReadOnlyList<string> limits = Array.AsReadOnly(new[] {
        "selected observations only; omitted/external/aliased/concurrent writers remain limits",
        "unchanged unobserved external effects require explicit invalidation",
        "value/index and inherited path/effect estimates are independent and retained",
        "terminal/self-stop observations do not establish scheduler/callee or cancellation-safe completion"
    });

    public ScummCompletionObserver(Action<ScummCompletionRead> receive, int slotLimit = 4096, int boundaryLimit = 128)
    {
        ArgumentNullException.ThrowIfNull(receive);
        if (slotLimit is < 1 or > 100_000) throw new ArgumentOutOfRangeException(nameof(slotLimit));
        if (boundaryLimit is < 1 or > 4096) throw new ArgumentOutOfRangeException(nameof(boundaryLimit));
        this.receive = receive;
        this.slotLimit = slotLimit;
        this.boundaryLimit = boundaryLimit;
    }

    public void Observe(ScummRuntimeRelationUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);
        // A missing/filtered/external stream boundary cannot bridge two invocations.
        if (update.Observation is null && update.Barrier is not null) continuities.Clear();
        foreach (var writer in update.Writes.Where(w => w.Value == 0 && w.Observation.Site?.Operation == "move"))
        {
            if (continuities.Count >= slotLimit) continuities.Clear();
            continuities[writer.Id] = new(writer);
        }
        if (update.Observation is { } observation)
        {
            if (observation.CancelledSlots is { } cancelled)
            {
                foreach (var slot in cancelled) ends.Remove(slot);
                foreach (var id in continuities.Where(p => cancelled.Contains(p.Value.Writer.Observation.Slot) ||
                    p.Value.Writer.Observation.ParentSlot is { } parent && cancelled.Contains(parent)).Select(p => p.Key).ToArray()) continuities.Remove(id);
            }
            foreach (var (id, continuity) in continuities.Where(p => p.Value.Writer.Observation.Slot == observation.Slot).ToArray())
                if (!Advance(continuity, observation)) continuities.Remove(id);
            if (observation.Site is { Stage: "end" })
            {
                if (ends.Count >= slotLimit) ends.Clear();
                ends[observation.Slot] = observation;
            }
        }
        foreach (var read in update.Reads.Where(r => r.Observation.Site?.Operation == "equalZero" &&
            r.Binding.Role == "condition" && r.Binding.Value == 0))
        {
            var writer = read.ObservedOrigin;
            var literal = writer?.Observation.OperandBindings?.SingleOrDefault(b => b.Role == "source");
            var rejection = writer is null ? "no surviving observed writer" :
                writer.Observation.Sequence >= read.Observation.Sequence || writer.ConfirmationSequence >= read.Observation.Sequence
                    ? "writer not confirmed before reader" :
                writer.Observation.Slot == read.Observation.Slot || writer.Observation.ParentSlot != read.Observation.Slot
                    ? "writer is not this reader's causal child" :
                writer.Observation.Site?.Operation != "move" || literal is not { Key: null, Index: null, Value: 0, UnresolvedReason: null }
                    ? "writer is not an observed literal zero move" : null;
            var writerEnd = writer is not null && ends.TryGetValue(writer.Observation.Slot, out var end) &&
                end.Sequence >= writer.ConfirmationSequence && end.Sequence < read.Observation.Sequence &&
                end.Site?.SameResource(writer.Observation.Site) == true ? end : null;
            var terminalRejection = writerEnd is null ? "no later source-consistent writer end" :
                writerEnd.Invocation != writer!.Observation.Invocation || writerEnd.ParentSlot != writer.Observation.ParentSlot
                    ? "writer end belongs to another invocation or causal parent" :
                writerEnd.ReachedStop != true || writerEnd.ResumeOffset is not null || writerEnd.UnresolvedReason is not null
                    ? "writer end is yielded, partial or unresolved" : null;
            ScummWriterTerminalContinuity? resumed = null;
            if (terminalRejection is not null && writer is not null && continuities.TryGetValue(writer.Id, out var chain) &&
                chain.Terminal is { } terminal && terminal.Sequence >= writer.ConfirmationSequence &&
                terminal.Sequence < read.Observation.Sequence && terminal.Invocation != writer.Observation.Invocation)
            {
                writerEnd = terminal;
                terminalRejection = null;
                resumed = new(writer.Id, Array.AsReadOnly(chain.Boundaries.ToArray()));
            }
            receive(new(read, writer, rejection is null, writerEnd, rejection, limits,
                terminalRejection is null, terminalRejection, resumed));
        }
    }

    private bool Advance(Continuity continuity, ScummScriptObservation observation)
    {
        var writer = continuity.Writer;
        if (observation.Sequence < writer.ConfirmationSequence) return true;
        if (observation.Site is not { } site || !site.SameResource(writer.Observation.Site) ||
            observation.ParentSlot != writer.Observation.ParentSlot || observation.UnresolvedReason is not null) return false;
        if (site.Stage == "begin")
        {
            if (continuity.Terminal is not null || continuity.ExpectedResume != site.Offset ||
                !continuity.Invocations.Add(observation.Invocation)) return false;
            continuity.Invocation = observation.Invocation;
            continuity.ExpectedResume = null;
        }
        else
        {
            if (continuity.Terminal is not null || continuity.ExpectedResume is not null || observation.Invocation != continuity.Invocation) return false;
            if (site.Stage != "end") return site.Stage == "instruction";
            if (observation.ReachedStop == true && observation.ResumeOffset is null) continuity.Terminal = observation;
            else if (observation.ReachedStop == false && observation.ResumeOffset is { } resume) continuity.ExpectedResume = resume;
            else return false;
        }
        if (continuity.Boundaries.Count >= boundaryLimit) return false;
        continuity.Boundaries.Add(observation);
        return true;
    }
}

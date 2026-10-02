namespace SCUMMexplorer.Core.Formats;

/// <summary>
/// A selected zero-read and its last surviving snapshot-confirmed literal writer, if any.
/// Matching the causal child is evidence about this stream, never protocol completion.
/// Writer/read value, index, inherited path and terminal observations remain independent.
/// </summary>
public sealed record LoomCompletionRead(LoomRuntimeRead Read, LoomRuntimeWrite? Writer,
    bool MatchesOwnLiteralChild, LoomScriptObservation? WriterEnd, string? Rejection,
    IReadOnlyList<string> Limits);

/// <summary>
/// Generic zero-wait observation over relation updates, without script IDs, room exceptions,
/// helper summaries or mutable runtime access. Connect Observe to a relation tracker's sink.
/// Filtered/unknown/external writes and cancellations have already retired live writer origins.
/// No observation influences playback, clears estimates or establishes scheduler/callee proof.
/// </summary>
public sealed class LoomCompletionObserver
{
    private readonly Action<LoomCompletionRead> receive;
    private readonly int slotLimit;
    private readonly Dictionary<long, LoomScriptObservation> ends = [];
    private static readonly IReadOnlyList<string> limits = Array.AsReadOnly(new[] {
        "selected observations only; omitted/external/aliased/concurrent writers remain limits",
        "unchanged unobserved external effects require explicit invalidation",
        "value/index and inherited path/effect estimates are independent and retained",
        "terminal/self-stop observations do not establish scheduler/callee or cancellation-safe completion"
    });

    public LoomCompletionObserver(Action<LoomCompletionRead> receive, int slotLimit = 4096)
    {
        ArgumentNullException.ThrowIfNull(receive);
        if (slotLimit is < 1 or > 100_000) throw new ArgumentOutOfRangeException(nameof(slotLimit));
        this.receive = receive;
        this.slotLimit = slotLimit;
    }

    public void Observe(LoomRuntimeRelationUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);
        if (update.Observation is { } observation)
        {
            if (observation.CancelledSlots is { } cancelled)
                foreach (var slot in cancelled) ends.Remove(slot);
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
            receive(new(read, writer, rejection is null,
                writer is not null && ends.TryGetValue(writer.Observation.Slot, out var end) &&
                    end.Sequence >= writer.ConfirmationSequence && end.Sequence < read.Observation.Sequence ? end : null,
                rejection, limits));
        }
    }
}

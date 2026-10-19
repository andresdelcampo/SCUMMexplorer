using System.Globalization;
using System.Text.RegularExpressions;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>A conditional decoded source pair, the native opening, or a separately entered room preview.</summary>
public enum ScummV1ColorContextBasis { SourcePath, Opening, RoomPreview }

/// <summary>
/// One display-only v1 colour context. A reached source operation is not proof that the actor
/// was visible, nor that the route to a preview was played. This never replaces ColorOf.
/// </summary>
public sealed record ScummV1CostumeColorContext(int Costume, int Skin, int Suit, int? Actor,
    string? ActorName, ScummV1ColorContextBasis Basis, bool IsEstimate, string Evidence, int? PreviewRoom = null);

/// <summary>
/// Reads adjacent source effects and observes the existing bounded session. It does not execute
/// script instructions itself, modify the user's session, or infer effects of a pending child.
/// DOS v1 suit colours retain the actor's own palette across costume changes (ScummVM
/// costume.cpp byleRLEDecode_C64, actor.cpp setActorCostume, script_v2.cpp o2_actorOps).
/// </summary>
internal sealed class ScummV1PaletteContexts(GameLibrary game,
    IReadOnlyList<(ScummScriptDisassembly Script, string Source, int? Room)> sources)
{
    private static readonly Regex Setting = new(
        @"^(0x[0-9A-F]+|var\[0x[0-9A-F]+\]), (0x[0-9A-F]+|var\[0x[0-9A-F]+\]), sub=0x(02|04)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex JumpTarget = new(@"->\s*0x([0-9A-F]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private readonly object gate = new();
    private IReadOnlyList<ScummV1CostumeColorContext>? opening;
    private readonly Dictionary<int, IReadOnlyList<ScummV1CostumeColorContext>> rooms = [];

    internal IReadOnlyList<ScummV1CostumeColorContext> ForCostume(int costume, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            opening ??= Run(null, cancellationToken);
            var result = new List<ScummV1CostumeColorContext>(opening.Where(c => c.Costume == costume));
            foreach (var source in sources)
                result.AddRange(SourcePairs(source.Script, source.Source).Where(c => c.Costume == costume));
            // A variable costume can select any resource. Let the game's own table/branch/operand
            // resolver decide it in an independent preview; never bake that table into this reader.
            var candidates = sources.Where(s => s.Room.HasValue && s.Script.Instructions.Any(i =>
            {
                if (i.Operation != "actorOps") return false;
                var match = Setting.Match(i.Operands);
                return match.Success && match.Groups[3].Value == "04" &&
                    (Literal(match.Groups[2].Value) is not { } value || value == costume);
            })).Select(s => s.Room!.Value).Distinct().Order();
            foreach (var room in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!rooms.TryGetValue(room, out var contexts))
                    rooms[room] = contexts = Run(room, cancellationToken);
                result.AddRange(contexts.Where(c => c.Costume == costume));
            }
            return result.DistinctBy(c => (c.Costume, c.Skin, c.Suit, c.Actor, c.Basis, c.IsEstimate, c.PreviewRoom)).ToArray();
        }
    }

    private static int? Literal(string operand) => operand.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
        && int.TryParse(operand.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value) ? value : null;

    /// <summary>Only consecutive actor operations inside one basic block can establish a local pair.</summary>
    internal static IReadOnlyList<ScummV1CostumeColorContext> SourcePairs(ScummScriptDisassembly script, string source)
    {
        var targets = script.Instructions.Select(i => JumpTarget.Match(i.Operands)).Where(m => m.Success)
            .Select(m => Convert.ToInt32(m.Groups[1].Value, 16)).ToHashSet();
        var states = new Dictionary<string, (int? Costume, int? Palette, int CostumeOffset, int PaletteOffset)>();
        var result = new List<ScummV1CostumeColorContext>();
        string? previousActor = null;
        int? nextOffset = null;
        foreach (var instruction in script.Instructions.OrderBy(i => i.Offset))
        {
            if (targets.Contains(instruction.Offset) || nextOffset is { } next && instruction.Offset != next) states.Clear();
            nextOffset = instruction.Offset + instruction.Bytes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            if (instruction.Operation != "actorOps") { states.Clear(); continue; }
            var match = Setting.Match(instruction.Operands);
            // Other actorOps include only sound/name/talk colour in v1. Conservatively do not
            // bridge an unknown actorOps shape or inline-name decoding boundary either.
            if (!match.Success) { states.Clear(); continue; }
            var actorOperand = match.Groups[1].Value.ToUpperInvariant();
            // A variable actor can alias a literal or another variable actor. Only identical
            // consecutive operands establish identity without resolving a live selector.
            if (actorOperand != previousActor) states.Clear();
            previousActor = actorOperand;
            var actor = Literal(actorOperand);
            if (actor is <= 0 || actor >= ScummActorTable.Count) { states.Clear(); continue; }
            var value = Literal(match.Groups[2].Value);
            var state = states.GetValueOrDefault(actorOperand);
            if (match.Groups[3].Value == "04") state = (value, state.Palette, instruction.Offset, state.PaletteOffset);
            else state = (state.Costume, value, state.CostumeOffset, instruction.Offset);
            states[actorOperand] = state;
            if (state is { Costume: > 0 and var knownCostume, Palette: { } palette })
                result.Add(new(knownCostume, 8, palette & 15, actor, null, ScummV1ColorContextBasis.SourcePath, false,
                    $"Conditional source path, not execution: {source}; unchanged actor operand {match.Groups[1].Value}; " +
                    $"costume @{state.CostumeOffset:X4}, palette @{state.PaletteOffset:X4}; no call, yield, write or control-flow join between them."));
        }
        return result;
    }

    private IReadOnlyList<ScummV1CostumeColorContext> Run(int? roomId, CancellationToken cancellationToken)
    {
        var timeline = new Timeline(cancellationToken);
        var observer = new ScummScriptObserver(timeline.Observe, site =>
        {
            // Filtering happens before numeric copies: cancellation observes all executed
            // instructions without capturing an entire script/state trace.
            cancellationToken.ThrowIfCancellationRequested();
            return site.Operation == "actorOps";
        });
        var data = new ScummV1DataSource(game);
        var session = new ScummGameSession(game, data.ReadIndexedObjects(), observer: observer, data: data);
        cancellationToken.ThrowIfCancellationRequested();
        if (session.BootReport?.HitFrameLimit != false) return [];
        if (roomId is int room)
        {
            var resource = game.Rooms.FirstOrDefault(r => r.ResourceId == room);
            if (resource is null) return [];
            timeline.BeginPreview(room);
            if (session.CurrentRoomId == room) session.EnterRoomAgain(resource);
            else session.VisitRoom(resource);
            session.Settle(); // The existing first-visit quiet/ambient rules and 2400-frame cap.
            cancellationToken.ThrowIfCancellationRequested();
        }
        // Final native state is an additional subset consistency check. Transient effects
        // rely on the supported, valid sub-02/04 operand semantics above; a later overwrite
        // alone cannot prove every earlier effect (hence the explicit indirection guards).
        return timeline.Complete(session.Actors);
    }

    internal sealed class Timeline(CancellationToken cancellationToken = default)
    {
        private sealed record State(int? Costume, int? Suit, bool CostumeEstimated, bool SuitEstimated,
            string CostumeSource, string PaletteSource);
        private readonly Dictionary<int, State> states = Enumerable.Range(1, ScummActorTable.Count - 1).ToDictionary(a => a,
            a => new State(a, 0, false, false, $"engine initial costume {a}", "engine initial palette 0"));
        private readonly List<ScummV1CostumeColorContext> contexts = [];
        private int? previewRoom;

        internal void BeginPreview(int room)
        {
            previewRoom = room;
            contexts.Clear();
        }

        internal void Observe(ScummScriptObservation observation)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (observation.Site is not { Stage: "instruction", Operation: "actorOps" } site) return;
            var match = Setting.Match(site.Operands ?? "");
            if (!match.Success) return;
            (int? Value, bool Estimated) Resolve(string operand)
            {
                if (Literal(operand) is { } literal) return (literal, false);
                if (!observation.Values.TryGetValue(operand, out var value)) return (null, true);
                var estimated = observation.Estimated.Contains(operand);
                // v1/v2 readVar(14..16) are native indirect operands; retain both
                // selector and selected-cell estimates, never the pointer as a colour.
                if (operand.ToUpperInvariant() is "VAR[0X00E]" or "VAR[0X00F]" or "VAR[0X010]")
                {
                    var selected = $"var[0x{value:X3}]";
                    if (!observation.Values.TryGetValue(selected, out value)) return (null, true);
                    estimated |= observation.Estimated.Contains(selected);
                }
                return (value, estimated);
            }
            var actor = Resolve(match.Groups[1].Value);
            var value = Resolve(match.Groups[2].Value);
            if (actor.Value is not { } number || !states.TryGetValue(number, out var state))
            {
                // A missing identity may alias any actor; never carry a stale source pair.
                foreach (var key in states.Keys.ToArray()) states[key] = states[key] with { Costume = null, Suit = null };
                return;
            }
            var origin = $"{site.Context ?? $"Script {site.Script:00}"} @{site.Offset:X4} (sequence {observation.Sequence})";
            var estimated = observation.PathEstimated || actor.Estimated || value.Estimated;
            state = match.Groups[3].Value == "04"
                ? state with { Costume = value.Value, CostumeEstimated = estimated, CostumeSource = origin }
                : state with { Suit = value.Value, SuitEstimated = estimated, PaletteSource = origin };
            states[number] = state;
            if (state is not { Costume: > 0 and var costume, Suit: { } suit }) return;
            var basis = previewRoom is null ? ScummV1ColorContextBasis.Opening : ScummV1ColorContextBasis.RoomPreview;
            var estimate = previewRoom is not null || state.CostumeEstimated || state.SuitEstimated;
            contexts.Add(new(costume, 8, suit & 15, number, null, basis, estimate,
                $"{(previewRoom is int room ? $"Room {room:00} state preview from native opening; route/prerequisites not played" : "Native opening source operations")}; " +
                $"actor {number}; costume from {state.CostumeSource}; retained palette from {state.PaletteSource}. " +
                "Source-state colour only; actor visibility and original-interpreter appearance were not established.", previewRoom));
        }

        internal IReadOnlyList<ScummV1CostumeColorContext> Complete(IReadOnlyList<ScummActorSnapshot> actors)
        {
            var invalid = states.Where(pair => actors.FirstOrDefault(a => a.Number == pair.Key) is not { } actor ||
                pair.Value.Costume is { } costume && actor.Look.Costume != costume ||
                pair.Value.Suit is { } suit && actor.Look.Palette[pair.Key] != suit).Select(p => p.Key).ToHashSet();
            return contexts.Where(c => c.Actor is { } actor && !invalid.Contains(actor)).ToArray();
        }
    }
}

namespace SCUMMexplorer.Core.Formats;

/// <summary>Explicit exploration inputs and bounded waits. No prerequisite state writes.</summary>
public sealed record LoomReplayStep(string Kind, int ObjectId = 0, int Verb = 0,
    int X = 0, int Y = 0, int Frames = 3000, string? EndBoundary = null, int? ExpectedRoom = null);

/// <summary>The audit and UI use the same frame-driven sequence, in one live session.</summary>
public sealed class LoomReplaySequence
{
    private readonly LoomExplorationSession _session;
    private readonly IReadOnlyList<LoomReplayStep> _steps;
    private int _index, _frames, _quiet, _traceStart, _begun, _checkedFrame = -1;
    public bool Complete => _index == _steps.Count && Failure is null;
    public string? Failure { get; private set; }
    public LoomObjectActionResult? LastAction { get; private set; }
    public int Stage => _index;
    /// <summary>When set, <see cref="Continue"/> sends nothing from this step on (used to run the leading steps unseen).</summary>
    public int? PauseAt { get; set; }

    /// <summary>
    /// Runs the first <paramref name="steps"/> steps to completion at once, frame by frame without showing them,
    /// so a later scene starts from the story state they really reach (for example a look change or a flag the
    /// earlier scenes set). The inputs and waits are the sequence's own; nothing is written. False if a stage fails.
    /// </summary>
    public bool RunSilently(int steps, int maxFrames = 40000)
    {
        PauseAt = steps;
        for (var frame = 0; frame < maxFrames && Continue(); frame++) { _session.AdvanceFrame(); FrameAdvanced(); }
        PauseAt = null;
        return Failure is null && _index >= steps;
    }

    public LoomReplaySequence(LoomExplorationSession session, IReadOnlyList<LoomReplayStep> steps)
    {
        if (steps.Count == 0 || steps.Any(s => s.Kind is not ("verb" or "click" or "script" or "visit" or "staff" or "place" or "actor" or "frames" or "scene") ||
            s.Frames < 1 || s.Frames > LoomExplorationSession.CutsceneFrameLimit ||
            s.Kind == "scene" && (s.EndBoundary is null || s.ExpectedRoom is null)))
            throw new ArgumentException("A sequence needs explicit inputs and bounded, source-established completion waits.", nameof(steps));
        _session = session; _steps = steps;
        _traceStart = session.SessionTrace.Count; _begun = session.CutscenesBegun;
    }

    /// <summary>Send the next input only after its preceding wait succeeds; true requests one frame.</summary>
    public bool Continue()
    {
        if (Failure is not null) return false;
        while (_index < _steps.Count)
        {
            if (PauseAt is int pause && _index >= pause) return false;
            var step = _steps[_index];
            if (step.Kind == "staff")
            {
                // The Game Status edit "staff held" (ObjectId 1 = held, 0 = not), a supported labelled setup for scenes that
                // expect Bobbin to carry the distaff (the story that gave it to him is not replayed).
                // The edit needs a settled session (no scene, draft, movement or dialogue): wait for it.
                if (!_session.GameStatus.CanEdit)
                    return _frames >= step.Frames ? Fail("The staff edit never became possible: the session did not settle.") : true;
                try { _session.UpdateStaffPossession(step.ObjectId != 0); }
                catch (InvalidOperationException ex) { return Fail($"The staff edit was refused: {ex.Message}"); }
                Next(); continue;
            }
            if (step.Kind == "place")
            {
                // A labelled setup: the ego at (X, Y) with the camera on it (see PlaceEgo); waits for a settled session like "staff".
                if (!_session.GameStatus.CanEdit)
                    return _frames >= step.Frames ? Fail("The ego placement never became possible: the session did not settle.") : true;
                try { _session.PlaceEgo(step.X, step.Y); }
                catch (InvalidOperationException ex) { return Fail($"The ego placement was refused: {ex.Message}"); }
                Next(); continue;
            }
            if (step.Kind == "actor")
            {
                // A labelled setup: actor ObjectId in the current room at (X, Y), or at object Verb's walk point (see PlaceActorInRoom);
                // EndBoundary names the game code the placement is taken from.
                if (!_session.GameStatus.CanEdit)
                    return _frames >= step.Frames ? Fail("The actor placement never became possible: the session did not settle.") : true;
                try { _session.PlaceActorInRoom(step.ObjectId, step.X, step.Y, step.Verb, step.EndBoundary ?? "the game's code"); }
                catch (InvalidOperationException ex) { return Fail($"The actor placement was refused: {ex.Message}"); }
                Next(); continue;
            }
            if (step.Kind == "visit")
            {
                // A catalog hop (ObjectId = room): an explicit exploration assumption that keeps the story state so far.
                _session.VisitRoomById(step.ObjectId);
                _traceStart = _session.SessionTrace.Count; _begun = _session.CutscenesBegun;
                Next(); continue;
            }
            if (step.Kind is "verb" or "click" or "script")
            {
                // "script": ObjectId is the room-local script number and X the start offset (a bonus preview).
                LastAction = step.Kind switch
                {
                    "verb" => _session.ActivateObjectVerb(step.ObjectId, step.Verb),
                    "script" => _session.RunLocalScriptAt(step.ObjectId, step.X),
                    _ => _session.Click(step.X, step.Y)
                };
                if (LastAction.Uncertainties.Any(t => t.Contains("not sent, because", StringComparison.Ordinal)))
                    return Fail("Sequence input was refused by the session.");
                Next(); continue;
            }
            if (step.Kind == "frames" && _frames >= step.Frames) { Next(); continue; }
            if (step.Kind == "scene")
            {
                bool idle = !_session.InCutscene && !_session.EgoMoving && !_session.CameraMoving && _session.Messages.HaveMsg == 0;
                if (_checkedFrame != _frames) { _quiet = idle ? _quiet + 1 : 0; _checkedFrame = _frames; }
                var trace = _session.SessionTrace.Skip(_traceStart).ToArray();
                bool ended = trace.Any(t => t.StartsWith(step.EndBoundary!, StringComparison.Ordinal));
                bool balanced = _session.CutscenesBegun > _begun &&
                    trace.Count(t => t.StartsWith("endCutscene at ", StringComparison.Ordinal)) == _session.CutscenesBegun - _begun;
                if (_quiet >= 32 && ended && balanced && _session.Snapshot.RoomId == step.ExpectedRoom)
                {
                    if (_session.Snapshot.Uncertainties.Any(t => t.Contains("unsupported", StringComparison.OrdinalIgnoreCase) || t.Contains("unmodelled", StringComparison.OrdinalIgnoreCase)))
                        return Fail("Sequence reached unsupported execution; later inputs were cancelled.");
                    _traceStart = _session.SessionTrace.Count; _begun = _session.CutscenesBegun;
                    Next(); continue;
                }
                if (_frames >= step.Frames)
                    return Fail($"Stage {_index + 1} did not reach balanced {step.EndBoundary} in Room {step.ExpectedRoom} within {step.Frames} frames; later inputs were cancelled.");
            }
            return true;
        }
        return false;
    }

    public void FrameAdvanced() => _frames++;
    private void Next() { _index++; _frames = 0; _quiet = 0; _checkedFrame = -1; }
    private bool Fail(string reason) { Failure = reason; return false; }
}

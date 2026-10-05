namespace SCUMMexplorer.Core.Formats;

/// <summary>How a message reaches the screen (ScummVM <c>printString</c>'s text slot).</summary>
public enum LoomMessageKind
{
    /// <summary>Text slot 0: <c>actorTalk</c>. It sets <c>VAR_HAVE_MSG</c> and lasts for the talk delay.</summary>
    Talk,
    /// <summary>Text slot 1 (<c>print 0xFE</c>): <c>drawString</c> draws it at once; it stays until the room is redrawn.</summary>
    Drawn
}

/// <summary>
/// A message on the screen, as a reached <c>print</c> left it: the text slot's
/// position, centring, line height and colour, and the text of the current
/// chunk (up to a wait code, <c>0xFF 0x03</c>) split at new lines (<c>0xFF 0x01</c>).
/// Each line holds the charset's glyph codes. Coordinates are screen
/// coordinates (0–319, 0–199), as ScummVM's charset uses them.
/// </summary>
/// <param name="LineHeight">The text slot's height (v3 subop 6); 0 means the font's height.</param>
/// <param name="TalkDelay">For a talk message, the talk delay set when this chunk was shown, in jiffies (1/60 s).</param>
/// <param name="Estimate">Why the message may differ from the game, or null when every value was reached.</param>
public sealed record LoomMessage(
    LoomMessageKind Kind, int Actor, int TextSlot, IReadOnlyList<byte[]> LineGlyphs,
    int X, int Y, bool Center, int LineHeight, int Right, int Color,
    int? RoomId, string SourceLabel, int Offset, int TalkDelay, string? Estimate)
{
    public IReadOnlyList<string> Lines => LineGlyphs.Select(LoomScummTextDecoder.DecodeGlyphs).ToArray();

    public string Text => string.Join("\n", Lines);

    public string Source => $"{SourceLabel} +0x{Offset:X4}";

    /// <summary>ScummVM's actor 0xFF: a line with no speaker.</summary>
    public bool IsSystemLine => Actor == LoomMessageTable.NoActor;

    /// <summary>Boxes (<c>drawBox</c>) drawn over this string after it: its pixels inside them are covered.</summary>
    public IReadOnlyList<LoomScreenBox> ErasedBy { get; init; } = [];

    /// <summary>The charset chosen by <c>charsetSet</c> when the <c>print</c> ran, or null if none was (the game's default then).</summary>
    public int? Charset { get; init; }

    /// <summary>The voice line that went with the print (CD game, <c>sayVoice</c>), or null.</summary>
    public LoomVoiceCue? Voice { get; init; }
}

/// <summary>
/// The CD game's <c>print</c> sub-opcode 8 (<c>sayVoice</c>): where the spoken line lies on the disc and how long it runs, both
/// in tenths of a second (ScummVM turns them into CD frames with 7.5 frames per unit, 75 frames per second). The offset counts from
/// the start of the disc, so the audio track starts at <see cref="DiscSeconds"/> minus the data tracks. Offset 0 with length 0
/// stops the voice. The message's own timing is not changed by it: the line waits as long as its text says.
/// </summary>
public readonly record struct LoomVoiceCue(int Offset, int Length)
{
    public bool IsStop => Offset == 0 && Length == 0;
    public double DiscSeconds => Offset / 10.0;
    public double Seconds => Length / 10.0;

    /// <summary>
    /// CD frames (75 per second, 7.5 per unit) between the start of the disc and the cue. ScummVM subtracts the first track's 22,500
    /// frames and the two-second lead-in (150 frames) to get the position in the audio track.
    /// </summary>
    public int TrackStartFrame => Math.Max(0, (int)(Offset * 7.5 - DataTrackFrames - LeadInFrames));

    /// <summary>The voice's run time in CD frames, five frames (a twentieth of a second) longer than the cue says, as ScummVM plays it.</summary>
    public int TrackFrameCount => (int)(Length * 7.5 + 5);

    public const int DataTrackFrames = 22500;
    public const int LeadInFrames = 150;
}

/// <summary>A <c>drawBox</c> rectangle in screen coordinates, corners inclusive.</summary>
public sealed record LoomScreenBox(int Left, int Top, int Right, int Bottom, int Color)
{
    public bool Contains(int x, int y) => x >= Left && x <= Right && y >= Top && y <= Bottom;

    /// <summary>
    /// Whether the box covers the whole string for any glyph widths: LOOM's charset 0
    /// glyphs are at most 8 pixels wide and 8 rows high (with the shadow, one more each).
    /// </summary>
    public bool SurelyCovers(LoomMessage message)
    {
        var width = message.LineGlyphs.Count == 0 ? 0 : message.LineGlyphs.Max(line => line.Length) * 8 + 1;
        // drawString centres the first line on x, but later lines start a whole width left of it.
        var left = message.Center ? message.X - (message.LineGlyphs.Count > 1 ? width : width / 2) : message.X;
        var lineHeight = message.LineHeight != 0 ? message.LineHeight : 8;
        var bottom = message.Y + Math.Max(0, message.LineGlyphs.Count - 1) * lineHeight + 8;
        return left >= Left && left + width <= Right + 1 && message.Y >= Top && bottom <= Bottom + 1;
    }

    /// <summary>Whether the box may touch any of the string's pixels (a generous bound on its lines' extent).</summary>
    public bool MayTouch(LoomMessage message)
    {
        var width = message.LineGlyphs.Count == 0 ? 0 : message.LineGlyphs.Max(line => line.Length) * 8 + 1;
        var left = message.Center ? message.X - width : message.X;
        var right = message.X + width;
        var lineHeight = message.LineHeight != 0 ? message.LineHeight : 8;
        var bottom = message.Y + Math.Max(0, message.LineGlyphs.Count - 1) * lineHeight + 9;
        return Left <= right && Right >= left && Top <= bottom && Bottom >= message.Y;
    }
}

/// <summary>One subcommand of <c>print</c> (ScummVM <c>decodeParseString</c>) with its operands read now; a null value is unknown.</summary>
public readonly record struct LoomPrintOperation(string Name, int? First = null, int? Second = null);

/// <summary>A reached <c>print</c>: who it prints for, its subcommands, and the text's bytes (null when it has no text subcommand).</summary>
/// <param name="Variables">The printing script's variables, globals and locals, as they are now (for text codes that show a variable).</param>
public sealed record LoomPrintRequest(
    int ActorToPrintFor, IReadOnlyList<LoomPrintOperation> Operations, byte[]? Text,
    string SourceLabel, int Offset, int? RoomId, string? Estimate,
    IReadOnlyDictionary<string, ushort>? Variables = null);

/// <summary>
/// ScummVM's message state for v3 (<c>string.cpp</c>, <c>actor.cpp</c>): the four
/// text slots with their v3 defaults, <c>actorTalk</c>, <c>displayDialog</c>,
/// <c>stopTalk</c>, <c>_talkDelay</c> and <c>_haveMsg</c> (which the frame copies
/// to <c>VAR_HAVE_MSG</c>, <c>var[0x003]</c>). Slot 1 (<c>print 0xFE</c>) is
/// <c>drawString</c>: drawn at once, with no delay.
/// </summary>
public sealed class LoomMessageTable
{
    /// <summary>ScummVM <c>_actorToPrintStrFor</c> 0xFF: no actor.</summary>
    public const int NoActor = 0xFF;
    /// <summary><c>VAR_DEFAULT_TALK_DELAY</c> does not exist in v3, so <c>displayDialog</c> uses 60 jiffies.</summary>
    public const int DefaultTalkDelay = 60;
    /// <summary><c>scummLoop</c> counts at most 15 jiffies per frame.</summary>
    public const int MaxFrameDelta = 15;
    /// <summary>ScummVM <c>_screenWidth - 1</c>, the slots' default right edge.</summary>
    public const int DefaultRight = 319;
    private readonly Slot[] _slots = Enumerable.Range(0, 4).Select(_ => new Slot()).ToArray();
    private readonly List<byte> _buffer = [];
    private readonly List<List<byte>> _shown = [];
    private readonly List<LoomMessage> _drawn = [];
    private readonly List<LoomMessage> _log = [];
    private int _bufferPos;
    private bool _keepText;
    private int _color = 15;
    private LoomPrintRequest? _source;
    private string? _estimate;
    private int? _charset;
    private LoomVoiceCue? _voice, _talkVoice;
    private readonly List<LoomVoiceCue> _voiceEvents = [];

    internal Action? NativeMutation { get; set; }

    /// <summary>Existing message estimates copied for observation; no independent field provenance.</summary>
    public string? WaitEstimate => Talk?.Estimate is { } shown && _estimate is { } original && shown != original
        ? original + "; " + shown : Talk?.Estimate ?? _estimate;

    /// <summary>ScummVM <c>_talkDelay</c> in jiffies; the frame counts it down.</summary>
    public int TalkDelay { get; private set; }

    /// <summary>ScummVM <c>_haveMsg</c>: 0 none, 0xFF text still to show, 1 shown and waiting for its delay.</summary>
    public int HaveMsg { get; private set; }

    /// <summary>ScummVM <c>getTalkingActor</c>: the actor that talks, or 0xFF for a system line or no talk.</summary>
    public int TalkingActor { get; private set; } = NoActor;

    /// <summary>The talk message on the screen now, or null.</summary>
    public LoomMessage? Talk { get; private set; }

    /// <summary>Strings <c>drawString</c> drew since the room was last drawn, oldest first.</summary>
    public IReadOnlyList<LoomMessage> Drawn => _drawn;

    /// <summary>Every message chunk that reached the screen, in order.</summary>
    public IReadOnlyList<LoomMessage> Log => _log;

    /// <summary>Every <c>sayVoice</c> a reached print issued, in order (the game starts or stops the voice when the print is decoded, text or not).</summary>
    public IReadOnlyList<LoomVoiceCue> VoiceEvents => _voiceEvents;

    /// <summary>Everything on the screen now: drawn strings, then the talk message.</summary>
    public IReadOnlyList<LoomMessage> OnScreen => Talk is null ? _drawn.ToArray() : [.. _drawn, Talk];

    /// <summary>
    /// Why the slots' carried values (v3 keeps each print's position, height and
    /// colour for the next one) may differ from the game: the route in between
    /// was not run. Cleared per value once a reached print sets it again.
    /// </summary>
    public void RouteSkipped(string reason)
    {
        NativeMutation?.Invoke();
        foreach (var slot in _slots)
        {
            slot.PositionEstimate ??= reason;
            slot.ColorEstimate ??= reason;
        }
    }

    /// <summary>
    /// <c>o5_print</c>/<c>o5_printEgo</c> then <c>decodeParseString</c> for v3.
    /// Returns a line for the script trace.
    /// </summary>
    public string Print(LoomPrintRequest request, LoomScriptRuntimeState runtime)
    {
        NativeMutation?.Invoke();
        var index = request.ActorToPrintFor switch { 252 => 3, 253 => 2, 254 => 1, _ => 0 };
        var slot = _slots[index];
        slot.LoadDefault();
        var estimates = new List<string>();
        _voice = null;
        if (request.Estimate is not null) estimates.Add(request.Estimate);
        foreach (var operation in request.Operations)
        {
            switch (operation.Name)
            {
                case "position":
                    if (operation.First is int x && operation.Second is int y)
                    {
                        (slot.X, slot.Y) = (x, y);
                        slot.PositionEstimate = null;
                    }
                    else slot.PositionEstimate = "the print's position operand could not be read";
                    slot.Overhead = false;
                    break;
                case "color":
                    if (operation.First is int color)
                    {
                        slot.Color = color;
                        slot.ColorEstimate = null;
                    }
                    else slot.ColorEstimate = "the print's colour operand could not be read";
                    break;
                case "clipped":
                    if (operation.First is int right) slot.Right = right;
                    break;
                case "center":
                    slot.Center = true;
                    slot.Overhead = false;
                    break;
                case "height":
                    // v3: subop 6 sets the line height (ScummVM SO_LEFT reads _string.height for version 3).
                    if (operation.First is int height) slot.Height = height;
                    else estimates.Add("the print's line height operand could not be read");
                    break;
                case "overhead":
                    slot.Overhead = true;
                    break;
                case "sayVoice":
                    if (operation.First is int voiceOffset && operation.Second is int voiceLength)
                    {
                        _voice = new LoomVoiceCue(voiceOffset, voiceLength);
                        _voiceEvents.Add(_voice.Value);
                    }
                    else estimates.Add("the print's voice operands could not be read");
                    break;
                default:
                    estimates.Add($"print subcommand {operation.Name} is not modelled");
                    break;
            }
        }
        if (request.Text is null)
        {
            slot.SaveDefault();
            return $"print for actor {request.ActorToPrintFor} sets text slot {index} and shows no text";
        }
        string trace;
        switch (index)
        {
            case 0:
                trace = ActorTalk(request, slot, runtime, estimates);
                break;
            case 1:
                trace = DrawString(request, slot, runtime, estimates);
                break;
            default:
                // Slot 2 is debugMessage and slot 3 showMessageDialog (a banner that waits for a key).
                trace = $"print for actor {request.ActorToPrintFor} uses text slot {index} ({(index == 2 ? "a debug message" : "a message dialog")}), which is not shown";
                break;
        }
        // v1–v3 keep position, height and colour as the slot's defaults, but not the centring (ScummVM decodeParseStringTextString).
        slot.SaveCarried();
        return trace;
    }

    private string ActorTalk(LoomPrintRequest request, Slot slot, LoomScriptRuntimeState runtime, List<string> estimates)
    {
        var buffer = LoomScummTextDecoder.ConvertMessage(request.Text!, request.Variables ?? runtime.GlobalVariables,
            runtime.ObjectName, out var conversionNote);
        if (conversionNote is not null) estimates.Add(conversionNote);
        var actor = request.ActorToPrintFor;
        if (actor == NoActor)
        {
            if (!_keepText) StopTalk();
            TalkingActor = NoActor;
        }
        else
        {
            if (!LoomActorTable.IsValid(actor))
                return $"print for actor {actor}, which is not a valid actor; ScummVM's derefActor would stop the game";
            var state = runtime.Actors[actor];
            if (state.Room is null)
                estimates.Add($"whether actor {actor} is in the current room is unknown ({state.Unknown ?? "its room is unknown"}); the text is shown as if it were");
            else if (state.Room != runtime.CurrentRoom)
                return $"print for actor {actor}, who is not in the current room: actorTalk shows nothing";
            if (!_keepText) StopTalk();
            TalkingActor = actor;
        }
        // The talking actor's talk colour, or the slot's colour for a line with no actor.
        if (TalkingActor == NoActor)
        {
            _color = slot.Color;
            if (slot.ColorEstimate is not null) estimates.Add($"the colour carries over from an earlier print: {slot.ColorEstimate}");
        }
        else _color = runtime.Actors.TalkColor(TalkingActor);
        if (slot.PositionEstimate is not null && !request.Operations.Any(item => item.Name == "position"))
            estimates.Add($"the position carries over from an earlier print: {slot.PositionEstimate}");
        if (slot.Overhead)
            estimates.Add("overhead text is not modelled; the slot's position is used");
        _buffer.Clear();
        _buffer.AddRange(buffer);
        _bufferPos = 0;
        _source = request;
        _charset = runtime.Charset;
        _talkVoice = _voice;
        _estimate = estimates.Count == 0 ? null : string.Join("; ", estimates.Distinct());
        _slotCopy = slot.Copy();
        TalkDelay = 0;
        HaveMsg = 0xFF;
        runtime.SetGlobalVariable(HaveMsgVariable, 0xFF);
        DisplayDialog(runtime);
        return Talk is null
            ? $"print for actor {actor}: no text shown"
            : $"message shown for actor {(actor == NoActor ? "none (0xFF)" : actor.ToString())}: \"{Talk.Text.Replace("\n", " / ", StringComparison.Ordinal)}\" at ({Talk.X}, {Talk.Y}){(Talk.Center ? " centred" : "")}, colour {Talk.Color}, talk delay {Talk.TalkDelay} jiffies{(Talk.Estimate is null ? "" : " (estimate)")}";
    }

    private Slot _slotCopy = new();

    private string DrawString(LoomPrintRequest request, Slot slot, LoomScriptRuntimeState runtime, List<string> estimates)
    {
        var buffer = LoomScummTextDecoder.ConvertMessage(request.Text!, request.Variables ?? runtime.GlobalVariables,
            runtime.ObjectName, out var conversionNote);
        if (conversionNote is not null) estimates.Add(conversionNote);
        if (slot.PositionEstimate is not null && !request.Operations.Any(item => item.Name == "position"))
            estimates.Add($"the position carries over from an earlier print: {slot.PositionEstimate}");
        if (slot.ColorEstimate is not null && !request.Operations.Any(item => item.Name == "color"))
            estimates.Add($"the colour carries over from an earlier print: {slot.ColorEstimate}");
        // drawString: new lines (codes 1 and 8) start a new line; codes 9, 10, 13 and 14 skip their operand.
        var lines = new List<List<byte>> { new() };
        for (var i = 0; i < buffer.Count; i++)
        {
            var c = buffer[i];
            if (c is 0xFF or 0xFE && i + 1 < buffer.Count)
            {
                var code = buffer[++i];
                if (code is 1 or 8) lines.Add([]);
                else if (code is 9 or 10 or 12 or 13 or 14) i += 2;
                continue;
            }
            lines[^1].Add(c);
        }
        var message = new LoomMessage(LoomMessageKind.Drawn, request.ActorToPrintFor, 1,
            lines.Select(line => line.ToArray()).ToArray(), slot.X, slot.Y, slot.Center, slot.Height, slot.Right, slot.Color,
            request.RoomId, request.SourceLabel, request.Offset, 0,
            estimates.Count == 0 ? null : string.Join("; ", estimates.Distinct())) { Charset = runtime.Charset, Voice = _voice };
        // drawString paints into the persistent screen, not a bounded message queue.
        // A new print cannot evict an unrelated score/staff glyph. Coalesce only
        // identical glyph geometry that this print completely paints again; different
        // strings may overlap only partly and must retain their uncovered pixels.
        _drawn.RemoveAll(previous => SameDrawnGlyphs(previous, message));
        _drawn.Add(message);
        _log.Add(message);
        return $"string drawn: \"{message.Text.Replace("\n", " / ", StringComparison.Ordinal)}\" at ({message.X}, {message.Y}), colour {message.Color}";
    }

    /// <summary>
    /// ScummVM <c>o5_drawBox</c>/<c>drawBox</c>: fills the rectangle (corners inclusive, in
    /// either order) on the screen it is in, so drawn strings under it are covered. Script 008
    /// erases the close-up and the object's name this way (a box at 232,152–312,192 in colour 0)
    /// before it shows the next object's. Each drawn string keeps the boxes drawn after it
    /// (<see cref="LoomMessage.ErasedBy"/>); one a box surely covers is dropped. The fill itself
    /// is not drawn: over the verb screen, colour 0 is its black.
    /// </summary>
    public string DrawBox(int x1, int y1, int x2, int y2, int color)
    {
        NativeMutation?.Invoke();
        var box = new LoomScreenBox(Math.Min(x1, x2), Math.Min(y1, y2), Math.Max(x1, x2), Math.Max(y1, y2), color);
        var covered = Cover(box);
        return $"box drawn at ({box.Left}, {box.Top})–({box.Right}, {box.Bottom}), colour {color}: " +
               (covered == 0 ? "no drawn string is under it" : $"{covered} drawn string(s) covered");
    }

    private static bool SameDrawnGlyphs(LoomMessage previous, LoomMessage next) =>
        previous.X == next.X && previous.Y == next.Y && previous.Center == next.Center &&
        previous.LineHeight == next.LineHeight && previous.Right == next.Right &&
        (previous.Color & 0xC0) == (next.Color & 0xC0) &&
        previous.LineGlyphs.Count == next.LineGlyphs.Count &&
        previous.LineGlyphs.Zip(next.LineGlyphs).All(pair => pair.First.AsSpan().SequenceEqual(pair.Second));

    /// <summary>
    /// A verb drawn or cleared on the verb screen (<see cref="LoomVerbTable.LastCover"/>): its
    /// opaque image, or its back colour, covers the drawn strings under it, as a box does.
    /// Script 027 prints the draft's note letters at y 188 and turns verb 0x35 on over them
    /// (96, 184) when a new draft starts; script 028 turns it off, which clears them.
    /// </summary>
    public string VerbDrawn(LoomScreenBox box)
    {
        NativeMutation?.Invoke();
        var covered = Cover(box);
        return covered == 0 ? "" : $"; {covered} drawn string(s) covered";
    }

    private int Cover(LoomScreenBox box)
    {
        var covered = 0;
        for (var i = _drawn.Count - 1; i >= 0; i--)
        {
            var drawn = _drawn[i];
            if (box.SurelyCovers(drawn))
            {
                _drawn.RemoveAt(i);
                covered++;
            }
            else if (box.MayTouch(drawn)) _drawn[i] = drawn with { ErasedBy = [.. drawn.ErasedBy, box] };
        }
        return covered;
    }

    /// <summary>
    /// The start of a frame (ScummVM <c>scummLoop</c> for v3): the talk delay goes
    /// down by the frame's jiffies (<c>VAR_TIMER_NEXT</c>, at least 1, at most 15),
    /// <c>displayDialog</c> shows the next chunk or ends the message, and
    /// <c>VAR_HAVE_MSG</c> takes <c>_haveMsg</c> before the scripts run.
    /// </summary>
    public void StartFrame(int delta, LoomScriptRuntimeState runtime)
    {
        NativeMutation?.Invoke();
        delta = Math.Clamp(delta, 1, MaxFrameDelta);
        TalkDelay = Math.Max(0, TalkDelay - delta);
        DisplayDialog(runtime);
        runtime.SetGlobalVariable(HaveMsgVariable, (ushort)HaveMsg);
    }

    /// <summary>
    /// ScummVM's talk-stop key ('.') sets the talk delay to 0, so the next frame
    /// ends the message or shows its next chunk. The explorer uses it to click
    /// text away: assumed input.
    /// </summary>
    public void SkipTalkDelay()
    {
        NativeMutation?.Invoke();
        TalkDelay = 0;
    }

    /// <summary>
    /// Runs an actor's talk frame (<c>runActorTalkScript</c>): true for the talk-start frame when a chunk of its
    /// text starts, false for the talk-stop frame at <c>stopTalk</c>. The session connects it to the actor table.
    /// </summary>
    public Action<int, bool>? TalkAnimation { get; set; }

    private bool _useTalkAnims;

    /// <summary><c>startScene</c>: <c>stopTalk</c>, and the room redraw erases drawn strings.</summary>
    public void RoomEntered(LoomScriptRuntimeState runtime)
    {
        NativeMutation?.Invoke();
        StopTalk();
        _drawn.Clear();
        runtime.SetGlobalVariable(HaveMsgVariable, 0);
    }

    /// <summary>ScummVM <c>stopTalk</c>: no message, no delay, no talking actor, and the text is erased.</summary>
    private void StopTalk()
    {
        NativeMutation?.Invoke();
        // stopTalk (v3): the talking actor in the current room goes back to its talk-stop frame.
        if (_useTalkAnims && TalkingActor != NoActor) TalkAnimation?.Invoke(TalkingActor, false);
        _useTalkAnims = false;
        HaveMsg = 0;
        TalkDelay = 0;
        TalkingActor = NoActor;
        _keepText = false;
        _shown.Clear();
        Talk = null;
    }

    /// <summary>ScummVM <c>displayDialog</c> for v3 (no line wrapping; the charset draws each character).</summary>
    private void DisplayDialog(LoomScriptRuntimeState runtime)
    {
        NativeMutation?.Invoke();
        if (HaveMsg == 0) return;
        if (TalkDelay > 0) return;
        if (HaveMsg == 1)
        {
            StopTalk();
            return;
        }
        // The talking actor starts its talk frame when text follows (LOOM DOS: not for an empty string).
        if (TalkingActor != NoActor && _bufferPos < _buffer.Count && _buffer[_bufferPos] != 0)
        {
            TalkAnimation?.Invoke(TalkingActor, true);
            _useTalkAnims = true;
        }
        TalkDelay = DefaultTalkDelay;
        if (!_keepText) _shown.Clear();
        if (_shown.Count == 0) _shown.Add([]);
        var charInc = runtime.GlobalVariables.TryGetValue(CharIncVariable, out var inc) ? (short)inc : 0;
        var estimates = new List<string>();
        while (true)
        {
            var (c, go) = NextCharsetCode(estimates);
            if (c == 0 && go)
            {
                // End of text: the message waits for its delay.
                HaveMsg = 1;
                _keepText = false;
                break;
            }
            if (!go) break;
            if (c == 13)
            {
                _shown.Add([]);
                continue;
            }
            _shown[^1].Add((byte)c);
            TalkDelay += charInc;
        }
        var source = _source!;
        var estimate = _estimate;
        if (estimates.Count > 0) estimate = string.Join("; ", new[] { estimate }.Concat(estimates).Where(item => item is not null).Distinct());
        Talk = new LoomMessage(LoomMessageKind.Talk, source.ActorToPrintFor == NoActor ? NoActor : TalkingActor, 0,
            _shown.Select(line => line.ToArray()).ToArray(), _slotCopy.X, _slotCopy.Y, _slotCopy.Center, _slotCopy.Height,
            _slotCopy.Right, _color, source.RoomId, source.SourceLabel, source.Offset, TalkDelay, estimate) { Charset = _charset, Voice = _talkVoice };
        _log.Add(Talk);
    }

    /// <summary>
    /// ScummVM <c>handleNextCharsetCode</c>: the next character (13 for a new
    /// line, 0 at the end), and false when a code ends this chunk (2: the text
    /// stays and the message ends; 3: wait, then the next chunk).
    /// </summary>
    private (int Code, bool Continue) NextCharsetCode(List<string> estimates)
    {
        while (true)
        {
            if (_bufferPos >= _buffer.Count) return (0, true);
            int c = _buffer[_bufferPos++];
            if (c is not (0xFF or 0xFE)) return (c, true);
            c = _bufferPos < _buffer.Count ? _buffer[_bufferPos++] : 0;
            switch (c)
            {
                case 1:
                    return (13, true);
                case 2:
                    HaveMsg = 0;
                    _keepText = true;
                    return (2, false);
                case 3:
                    HaveMsg = 0xFF;
                    _keepText = false;
                    return (3, false);
                case 8:
                    break;
                case 9:
                    estimates.Add("a talk animation code (startAnimActor) in the text is not modelled");
                    _bufferPos += 2;
                    break;
                case 12:
                {
                    var color = _buffer[_bufferPos] | (_buffer[_bufferPos + 1] << 8);
                    _bufferPos += 2;
                    estimates.Add($"a colour code in the text (colour {color}) is not modelled; the whole text keeps the message colour");
                    break;
                }
                case 10 or 13 or 14:
                    estimates.Add($"text code {c} is not modelled");
                    _bufferPos += 2;
                    break;
                default:
                    return (0, true);
            }
        }
    }

    /// <summary><c>VAR_HAVE_MSG</c> (v3 var 3).</summary>
    public const string HaveMsgVariable = "var[0x003]";
    /// <summary><c>VAR_CHARINC</c> (v3 var 37): jiffies per character; script 001 +0x0012 sets 5.</summary>
    public const string CharIncVariable = "var[0x025]";

    private sealed class Slot
    {
        // ScummVM resetScumm for version 3: default position (0, 0), right 319, height 0, colour 15, not centred.
        private int _defaultX, _defaultY, _defaultHeight, _defaultColor = 15, _defaultRight = DefaultRight;
        private bool _defaultCenter, _defaultOverhead;
        public int X, Y, Height, Color = 15, Right = DefaultRight;
        public bool Center, Overhead;
        public string? PositionEstimate, ColorEstimate;

        public void LoadDefault()
        {
            (X, Y, Height, Color, Right, Center, Overhead) =
                (_defaultX, _defaultY, _defaultHeight, _defaultColor, _defaultRight, _defaultCenter, _defaultOverhead);
        }

        public void SaveDefault()
        {
            (_defaultX, _defaultY, _defaultHeight, _defaultColor, _defaultRight, _defaultCenter, _defaultOverhead) =
                (X, Y, Height, Color, Right, Center, Overhead);
        }

        public void SaveCarried() =>
            (_defaultX, _defaultY, _defaultHeight, _defaultColor) = (X, Y, Height, Color);

        public Slot Copy() => (Slot)MemberwiseClone();
    }
}

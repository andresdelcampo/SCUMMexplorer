using System.Buffers.Binary;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// Plays the <c>ADL </c> part of a v5 sound (<see cref="ScummImuseSong"/>) on the OPL2, and renders it to a stereo wave. This is ScummVM's iMUSE AdLib driver
/// (<c>audio/adlib.cpp</c>, OPL2 mode, the version-5 volume algorithm) ported as it is: nine voices taken by note and priority, an instrument of 30 bytes per part
/// (the 0x7D 16 exclusive), two envelope generators per voice (the instrument's "extra" bytes), a voice timer of 250 Hz x 0xD69 / 0x411B, pitch from a note, a
/// bend and a detune. The iMUSE player half follows <c>imuse_player.cpp</c> as far as a song needs it: part set-up (0x7D 0), the controllers, unconditional
/// hooks and the loop point (see <see cref="ScummImuseMt32"/>, which takes the song the same way). A GM program change plays nothing here (the driver would
/// load a General MIDI timbre, which the game's AdLib music never relies on).
/// </summary>
public sealed class ScummImuseAdlib
{
    private const int SampleRate = 49716;
    private const int CallbackRate = 250;
    private const double ReleaseSeconds = 2;

    public sealed record Result(byte[] Wave, bool Loops, double Seconds);

    public byte[] RenderWave(byte[] part, CancellationToken cancellationToken = default) => Render(ScummImuseSong.Parse(part), cancellationToken).Wave;

    public Result Render(ScummImuseSong song, CancellationToken cancellationToken = default)
    {
        var chip = new NukedOplChip(SampleRate);
        var driver = new Driver(chip);
        var player = new Player(driver);

        var samples = new List<short>();
        var frameRemainder = 0;
        var index = 0;
        // The song as the game plays it on its own: jumps taken, a looping song unrolled (see ScummImuseTimeline).
        var timeline = ScummImuseTimeline.Build(song);
        var loops = timeline.Loops;
        var items = timeline.Items;
        var end = timeline.EndSeconds; // the second at which the last pass ends (the song's end, or the loop point)
        var maxSteps = (int)((ScummImuseTimeline.MaxSeconds + ReleaseSeconds + 1) * CallbackRate);

        for (var step = 0; step < maxSteps; step++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var now = step / (double)CallbackRate;
            // The iMUSE timer runs first, then the driver's voice timer.
            while (index < items.Count && items[index].Seconds <= now + 1e-9) player.Handle(items[index++].Event);
            if (now >= end + ReleaseSeconds) break;
            driver.OnTimer();
            frameRemainder += SampleRate;
            var frames = frameRemainder / CallbackRate;
            frameRemainder %= CallbackRate;
            var buffer = new short[frames * 2];
            chip.GenerateStream(buffer);
            samples.AddRange(buffer);
        }
        // Trim a silent tail so the wave does not end in a long pause.
        var last = samples.Count - 1;
        while (last > 0 && Math.Abs((int)samples[last]) <= 8) last--;
        var keep = Math.Min(samples.Count, last + 1 + SampleRate / 4 * 2);
        if (keep < samples.Count) samples.RemoveRange(keep, samples.Count - keep);
        if (samples.Count == 0 || samples.All(sample => sample == 0)) throw new InvalidDataException("The AdLib music produced no audio.");
        return new Result(ToWave(samples), loops, samples.Count / 2.0 / SampleRate);
    }

    private static byte[] ToWave(List<short> samples) => ScummMt32Audio.ToWave(samples.ToArray(), SampleRate);

    // ---------------------------------------------------------------- iMUSE player half

    private sealed class Player(Driver driver)
    {
        private readonly PartState?[] _parts = new PartState?[16];
        private readonly byte[]?[] _globalInstruments = new byte[]?[32];

        /// <summary>Part::load_global_instrument / copyGlobalInstrument: the global instrument of a slot, or the all-zero default the original uses.</summary>
        private void LoadGlobalInstrument(PartState state, int slot)
        {
            if (slot >= 32) return;
            var instrument = new byte[30];
            _globalInstruments[slot]?.CopyTo(instrument, 0);
            state.Mc.Instrument = instrument;
            state.InstrumentValid = true;
        }

        /// <summary>Handles an event; returns the target tick of a hook that jumps (id 0), or null.</summary>
        public long? Handle(ScummImuseEvent e)
        {
            if (e.IsSysex) return Sysex(e.Data);
            var state = _parts[e.Channel];
            if (state is null) return null;
            var part = state.Mc;
            switch (e.Command)
            {
                case 0x8: driver.PartKeyOff(part, e.Data[0]); break;
                case 0x9:
                    if (e.Data[1] == 0) driver.PartKeyOff(part, e.Data[0]);
                    else if (state.On && state.InstrumentValid) driver.PartKeyOn(part, part.Instrument, e.Data[0], e.Data[1]);
                    break;
                case 0xB: Controller(state, e.Data[0], e.Data[1]); break;
                case 0xC: LoadGlobalInstrument(state, e.Data[0]); break;
                case 0xE: part.PitchBend(((e.Data[0] | e.Data[1] << 7) - 0x2000)); break;
            }
            return null;
        }

        private void Controller(PartState state, int control, int value)
        {
            var part = state.Mc;
            switch (control)
            {
                case 1: part.ModulationWheel((byte)value); break;
                case 7: state.Vol = (byte)value; part.Volume(EffectiveVolume(value)); break;
                case 16: if (value <= 12) { part.PitchBend(0); part.PitchBendFactor((byte)value); } break;
                case 17: state.Detune = (sbyte)(value - 0x40); part.Detune((short)Math.Clamp((int)state.Detune, -128, 127)); break;
                case 18: state.Pri = (sbyte)(value - 0x40); part.Priority((byte)Math.Clamp((int)state.Pri, 0, 255)); break;
                case 64: part.Sustain(value != 0); break;
                case 123: part.AllNotesOff(); break;
            }
        }

        private static int EffectiveVolume(int volume) => (volume + 1) * 127 >> 7;

        private long? Sysex(byte[] d)
        {
            if (d.Length < 2 || d[0] != 0x7D) return null;
            switch (d[1])
            {
                case 0: // set up a part
                {
                    if (d.Length < 3) break;
                    var chan = d[2] & 0x0F;
                    var buf = ScummImuseSong.Decode(d.AsSpan(3), 8).Prepend((byte)chan).ToArray(); // ScummVM's buf[1..8]
                    if (buf.Length < 9) break;
                    var state = _parts[chan] ??= new PartState(new Part(driver));
                    var part = state.Mc;
                    var on = (buf[1] & 1) != 0;
                    if (state.On != on)
                    {
                        state.On = on;
                        if (!on) { part.Sustain(false); part.AllNotesOff(); }
                    }
                    state.Pri = (sbyte)buf[2];
                    state.Vol = buf[3];
                    state.Transpose = (sbyte)buf[5]; // the part's own transpose; -128 (0x80) means none
                    state.Detune = (sbyte)buf[6];
                    LoadGlobalInstrument(state, buf[8]);
                    // Part::sendAll
                    if (buf[7] <= 12) { part.PitchBend(0); part.PitchBendFactor(buf[7]); }
                    part.Transpose(EffectiveTranspose(state, 12)); // the original set-up always clips to -12/12
                    part.Detune(state.Detune);
                    part.Volume(EffectiveVolume(state.Vol));
                    part.Priority((byte)Math.Clamp((int)state.Pri, 0, 255));
                    break;
                }
                case 1:
                    if (d.Length > 2 && _parts[d[2] & 0x0F] is { } off) { off.On = false; off.Mc.Sustain(false); off.Mc.AllNotesOff(); }
                    break;
                case 16: // AdLib instrument definition (part)
                {
                    if (d.Length < 4) break;
                    var chan = d[2] & 0x0F;
                    var state = _parts[chan] ??= new PartState(new Part(driver));
                    if (d.Length == 64 || d.Length == 50)
                    {
                        var raw = ScummImuseSong.Decode(d.AsSpan(4), d.Length - 4 >> 1);
                        var instrument = new byte[30];
                        raw.AsSpan(0, Math.Min(30, raw.Length)).CopyTo(instrument);
                        state.Mc.Instrument = instrument;
                        state.InstrumentValid = true;
                    }
                    break;
                }
                case 17: // AdLib instrument definition (global)
                    if (d.Length == 65 && d[4] < 32)
                    {
                        var instrument = new byte[30];
                        ScummImuseSong.Decode(d.AsSpan(5), 30).CopyTo(instrument, 0);
                        _globalInstruments[d[4] & 31] = instrument;
                    }
                    break;
                // Hooks that jump and the loop (48, 80) move the song's position: ScummImuseTimeline has already played them out.
                // The other hooks (49 to 53) act at once when their id is 0 (ScummVM maybe_set_transpose, maybe_part_onoff, ...); one with another id waits for a script's setHook and is not taken.
                case 49 when d.Length > 3: // global transpose
                {
                    var b = ScummImuseSong.Decode(d.AsSpan(3), 3);
                    if (b.Length < 3 || b[0] != 0) break;
                    SetTranspose(b[1], (sbyte)b[2]);
                    break;
                }
                case 50 when d.Length > 3: // part on/off
                {
                    var b = ScummImuseSong.Decode(d.AsSpan(3), 2);
                    if (b.Length < 2 || b[0] != 0 || _parts[d[2] & 0x0F] is not { } state) break;
                    var on = b[1] != 0;
                    if (state.On != on)
                    {
                        state.On = on;
                        if (!on) { state.Mc.Sustain(false); state.Mc.AllNotesOff(); }
                    }
                    break;
                }
                case 51 when d.Length > 3: // part volume
                {
                    var b = ScummImuseSong.Decode(d.AsSpan(3), 2);
                    if (b.Length < 2 || b[0] != 0 || _parts[d[2] & 0x0F] is not { } state) break;
                    state.Vol = b[1];
                    state.Mc.Volume(EffectiveVolume(state.Vol));
                    break;
                }
                case 52 when d.Length > 3: // part program
                {
                    var b = ScummImuseSong.Decode(d.AsSpan(3), 2);
                    if (b.Length < 2 || b[0] != 0 || _parts[d[2] & 0x0F] is not { } state) break;
                    LoadGlobalInstrument(state, b[1]);
                    break;
                }
                case 53 when d.Length > 3: // part transpose
                {
                    var b = ScummImuseSong.Decode(d.AsSpan(3), 3);
                    if (b.Length < 3 || b[0] != 0 || _parts[d[2] & 0x0F] is not { } state) break;
                    var value = (sbyte)b[2];
                    if (value > 24 || value < -24) break;
                    if (b[1] != 0) value = (sbyte)Math.Clamp(value + (state.Transpose == -128 ? 0 : state.Transpose), -7, 7);
                    state.Transpose = value;
                    state.Mc.Transpose(EffectiveTranspose(state, 24));
                    break;
                }
            }
            return null;
        }

        private int _transpose; // the player's global transpose (Player::_transpose)

        /// <summary>Part::set_transpose: the part's transpose plus the player's, clipped; -128 is "none".</summary>
        private sbyte EffectiveTranspose(PartState state, int limit) => state.Transpose == -128 ? (sbyte)0 : (sbyte)Math.Clamp(state.Transpose + _transpose, -limit, limit);

        /// <summary>Player::setTranspose: -24..24; relative adds to the current one (clipped to -7..7); every part is re-clipped to -24..24 (AdLib and PC speaker).</summary>
        private void SetTranspose(int relative, int value)
        {
            if (value > 24 || value < -24 || relative > 1) return;
            if (relative != 0) value = Math.Clamp(_transpose + value, -7, 7);
            _transpose = value;
            foreach (var state in _parts) state?.Mc.Transpose(EffectiveTranspose(state, 24));
        }
    }

    private sealed class PartState(Part mc)
    {
        public Part Mc { get; } = mc;
        public bool On;
        public bool InstrumentValid;
        public sbyte Pri;
        public byte Vol;
        public int Transpose;
        public sbyte Detune;
    }

    // ---------------------------------------------------------------- AdLib driver

    private sealed class Struct10
    {
        public byte Active;
        public short CurVal, Count;
        public ushort MaxValue;
        public short StartValue;
        public byte Loop;
        public byte[] TableA = new byte[4], TableB = new byte[4];
        public sbyte Unk3, ModWheel, ModWheelLast;
        public ushort SpeedLoMax, NumSteps;
        public short SpeedHi;
        public sbyte Direction;
        public ushort SpeedLo, SpeedLoCounter;
    }

    private sealed class Struct11
    {
        public short ModifyVal;
        public byte Param, Flag0x40, Flag0x10;
        public Struct10 S10 = null!;
    }

    private sealed class Voice
    {
        public Part? Part;
        public Voice? Next, Prev;
        public bool WaitForPedal;
        public byte Note, Channel;
        public bool TwoChan;
        public byte Vol1, Vol2;
        public short Duration;
        public readonly Struct10 S10a = new(), S10b = new();
        public readonly Struct11 S11a = new(), S11b = new();
    }

    private sealed class Part(Driver owner)
    {
        public Voice? Voice;
        public short PitchBendValue;
        public byte PitchBendFactorValue = 2;
        public sbyte TransposeEff;
        public byte VolEff;
        public short DetuneEff;
        public byte ModWheel;
        public bool Pedal;
        public byte PriEff = 127;
        public byte[] Instrument = new byte[30];

        public void PitchBend(int bend)
        {
            PitchBendValue = (short)bend;
            for (var voice = Voice; voice is not null; voice = voice.Next)
                owner.AdlibNoteOn(voice.Channel, voice.Note + TransposeEff, (PitchBendValue * PitchBendFactorValue >> 6) + DetuneEff);
        }

        public void ModulationWheel(byte value)
        {
            ModWheel = value;
            for (var voice = Voice; voice is not null; voice = voice.Next)
            {
                if (voice.S10a.Active != 0 && voice.S11a.Flag0x40 != 0) voice.S10a.ModWheel = (sbyte)(ModWheel >> 2);
                if (voice.S10b.Active != 0 && voice.S11b.Flag0x40 != 0) voice.S10b.ModWheel = (sbyte)(ModWheel >> 2);
            }
        }

        public void Volume(int value)
        {
            VolEff = (byte)value;
            for (var voice = Voice; voice is not null; voice = voice.Next)
            {
                owner.AdlibSetParam(voice.Channel, 0, VolumeTable[VolumeLookup[voice.Vol2, VolEff >> 2]]);
                if (voice.TwoChan) owner.AdlibSetParam(voice.Channel, 13, VolumeTable[VolumeLookup[voice.Vol1, VolEff >> 2]]);
            }
        }

        public void PitchBendFactor(byte value)
        {
            PitchBendFactorValue = value;
            for (var voice = Voice; voice is not null; voice = voice.Next)
                owner.AdlibNoteOn(voice.Channel, voice.Note + TransposeEff, (PitchBendValue * PitchBendFactorValue >> 6) + DetuneEff);
        }

        public void Detune(short value)
        {
            DetuneEff = value;
            for (var voice = Voice; voice is not null; voice = voice.Next)
                owner.AdlibNoteOn(voice.Channel, voice.Note + TransposeEff, (PitchBendValue * PitchBendFactorValue >> 6) + DetuneEff);
        }

        public void Transpose(sbyte value)
        {
            TransposeEff = value;
            for (var voice = Voice; voice is not null; voice = voice.Next)
                owner.AdlibNoteOn(voice.Channel, voice.Note + TransposeEff, (PitchBendValue * PitchBendFactorValue >> 6) + DetuneEff);
        }

        public void Priority(byte value) => PriEff = value;

        public void Sustain(bool value)
        {
            Pedal = value;
            if (value) return;
            for (var voice = Voice; voice is not null; voice = voice.Next)
                if (voice.WaitForPedal) owner.McOff(voice);
        }

        public void AllNotesOff()
        {
            while (Voice is not null) owner.McOff(Voice);
        }
    }

    private static readonly byte[] Operator1Offsets = [0, 1, 2, 8, 9, 10, 16, 17, 18];
    private static readonly byte[] Operator2Offsets = [3, 4, 5, 11, 12, 13, 19, 20, 21];

    // register base, shift, mask, inversion
    private static readonly byte[][] SetParams =
    [
        [0x40, 0, 63, 63], [0xE0, 2, 0, 0], [0x40, 6, 192, 0], [0x20, 0, 15, 0], [0x60, 4, 240, 15], [0x60, 0, 15, 15], [0x80, 4, 240, 15],
        [0x80, 0, 15, 15], [0xE0, 0, 3, 0], [0x20, 7, 128, 0], [0x20, 6, 64, 0], [0x20, 5, 32, 0], [0x20, 4, 16, 0], [0xC0, 0, 1, 0], [0xC0, 1, 14, 0],
    ];

    private static readonly byte[] ParamTable1 = [29, 28, 27, 0, 3, 4, 7, 8, 13, 16, 17, 20, 21, 30, 31, 0];
    private static readonly ushort[] MaxValTable = [0x2FF, 0x1F, 0x7, 0x3F, 0x0F, 0x0F, 0x0F, 0x3, 0x3F, 0x0F, 0x0F, 0x0F, 0x3, 0x3E, 0x1F, 0];
    private static readonly ushort[] NumStepsTable =
    [
        1, 2, 4, 5, 6, 7, 8, 9, 10, 12, 14, 16, 18, 21, 24, 30, 36, 50, 64, 82, 100, 136, 160, 192, 240, 276, 340, 460, 600, 860, 1200, 1600,
    ];

    private static readonly byte[] NoteFrequencies =
    [
        90, 91, 92, 92, 93, 94, 94, 95, 96, 96, 97, 98, 98, 99, 100, 101, 101, 102, 103, 104, 104, 105, 106, 107, 107, 108, 109, 110, 111, 111, 112, 113,
        114, 115, 115, 116, 117, 118, 119, 120, 121, 121, 122, 123, 124, 125, 126, 127, 128, 129, 130, 131, 132, 132, 133, 134, 135, 136, 137, 138, 139, 140, 141, 142,
        143, 145, 146, 147, 148, 149, 150, 151, 152, 153, 154, 155, 157, 158, 159, 160, 161, 162, 163, 165, 166, 167, 168, 169, 171, 172, 173, 174, 176, 177, 178, 180,
        181, 182, 184, 185, 186, 188, 189, 190, 192, 193, 194, 196, 197, 199, 200, 202, 203, 205, 206, 208, 209, 211, 212, 214, 215, 217, 218, 220, 222, 223, 225, 226,
        228, 230, 231, 233, 235, 236, 238, 240, 242, 243, 245, 247, 249, 251, 252, 254,
    ];

    private static readonly byte[] VolumeTable =
    [
        0, 4, 7, 11, 13, 16, 18, 20, 22, 24, 26, 27, 29, 30, 31, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 44, 45, 46, 47, 47,
        48, 49, 49, 50, 51, 51, 52, 53, 53, 54, 54, 55, 55, 56, 56, 57, 57, 58, 58, 59, 59, 60, 60, 60, 61, 61, 62, 62, 62, 63, 63, 63,
    ];

    private static readonly byte[,] VolumeLookup = CreateLookupTable();

    private static byte[,] CreateLookupTable()
    {
        var table = new byte[64, 32];
        for (var i = 0; i < 64; i++)
        {
            var sum = i;
            for (var j = 0; j < 32; j++) { table[i, j] = (byte)(sum >> 5); sum += i; }
        }
        for (var i = 0; i < 64; i++) table[i, 0] = 0;
        return table;
    }

    private static int LookupVolume(int a, int b)
    {
        if (b == 0) return 0;
        if (b == 31) return a;
        if (a < -63 || a > 63) return b * (a + 1) >> 5;
        if (b < 0) return a < 0 ? VolumeLookup[-a, -b] : -VolumeLookup[a, -b];
        return a < 0 ? -VolumeLookup[-a, b] : VolumeLookup[a, b];
    }

    private sealed class Driver
    {
        private readonly NukedOplChip _opl;
        private readonly byte[] _regCache = new byte[256];
        private readonly ushort[] _channelTable2 = new ushort[9];
        private readonly ushort[] _curNotTable = new ushort[9];
        private readonly Voice[] _voices = new Voice[9];
        private int _voiceIndex = -1;
        private int _timerCounter;
        private byte _randSeed = 1;

        public Driver(NukedOplChip opl)
        {
            _opl = opl;
            for (var i = 0; i < 9; i++)
            {
                _voices[i] = new Voice { Channel = (byte)i };
            }
            foreach (var voice in _voices)
            {
                voice.S11a.S10 = voice.S10b;
                voice.S11b.S10 = voice.S10a;
            }
            AdlibWrite(8, 0x40);
            AdlibWrite(0xBD, 0x00);
            AdlibWrite(1, 0x20);
        }

        public void OnTimer()
        {
            _timerCounter += 0xD69;
            while (_timerCounter >= 0x411B)
            {
                _timerCounter -= 0x411B;
                foreach (var voice in _voices)
                {
                    if (voice.Part is null) continue;
                    if (voice.Duration != 0 && (voice.Duration -= 0x11) <= 0)
                    {
                        McOff(voice);
                        return; // as the original: the rest of this callback is skipped
                    }
                    if (voice.S10a.Active != 0) McIncStuff(voice, voice.S10a, voice.S11a);
                    if (voice.S10b.Active != 0) McIncStuff(voice, voice.S10b, voice.S11b);
                }
            }
        }

        private void AdlibWrite(int reg, int value)
        {
            var b = (byte)value;
            if (_regCache[reg] == b) return;
            _regCache[reg] = b;
            _opl.WriteRegister((ushort)reg, b);
        }

        private byte Reg(int reg) => _regCache[reg];

        public void McOff(Voice voice)
        {
            AdlibKeyOff(voice.Channel);
            var tmp = voice.Prev;
            if (voice.Next is not null) voice.Next.Prev = tmp;
            if (tmp is not null) tmp.Next = voice.Next;
            else voice.Part!.Voice = voice.Next;
            voice.Part = null;
        }

        private void McIncStuff(Voice voice, Struct10 s10, Struct11 s11)
        {
            var part = voice.Part!;
            var code = Struct10OnTimer(s10, s11);
            if ((code & 1) != 0)
            {
                switch (s11.Param)
                {
                    case 0:
                        voice.Vol2 = (byte)(s10.StartValue + s11.ModifyVal);
                        AdlibSetParam(voice.Channel, 0, VolumeTable[VolumeLookup[voice.Vol2 & 63, part.VolEff >> 2]]);
                        break;
                    case 13:
                        voice.Vol1 = (byte)(s10.StartValue + s11.ModifyVal);
                        if (voice.TwoChan) AdlibSetParam(voice.Channel, 13, VolumeTable[VolumeLookup[voice.Vol1 & 63, part.VolEff >> 2]]);
                        else AdlibSetParam(voice.Channel, 13, voice.Vol1);
                        break;
                    case 30: s11.S10.ModWheel = (sbyte)s11.ModifyVal; break;
                    case 31: s11.S10.Unk3 = (sbyte)s11.ModifyVal; break;
                    default: AdlibSetParam(voice.Channel, s11.Param, s10.StartValue + s11.ModifyVal); break;
                }
            }
            if ((code & 2) != 0 && s11.Flag0x10 != 0) AdlibKeyOnOff(voice.Channel);
        }

        private void AdlibKeyOff(int chan)
        {
            var reg = chan + 0xB0;
            AdlibWrite(reg, Reg(reg) & ~0x20);
        }

        private byte Struct10OnTimer(Struct10 s10, Struct11 s11)
        {
            byte result = 0;
            if (s10.Count != 0 && (s10.Count -= 17) <= 0)
            {
                s10.Active = 0;
                return 0;
            }
            var i = s10.CurVal + s10.SpeedHi;
            s10.SpeedLoCounter = (ushort)(s10.SpeedLoCounter + s10.SpeedLo);
            if (s10.SpeedLoCounter >= s10.SpeedLoMax)
            {
                s10.SpeedLoCounter = (ushort)(s10.SpeedLoCounter - s10.SpeedLoMax);
                i += s10.Direction;
            }
            if (s10.CurVal != i || s10.ModWheel != s10.ModWheelLast)
            {
                s10.CurVal = (short)i;
                s10.ModWheelLast = s10.ModWheel;
                i = LookupVolume(i, s10.ModWheelLast);
                if (i != s11.ModifyVal)
                {
                    s11.ModifyVal = (short)i;
                    result = 1;
                }
            }
            s10.NumSteps = (ushort)(s10.NumSteps - 1);
            if (s10.NumSteps == 0)
            {
                s10.Active++;
                if (s10.Active > 4)
                {
                    if (s10.Loop != 0)
                    {
                        s10.Active = 1;
                        result |= 2;
                        Struct10Setup(s10);
                    }
                    else s10.Active = 0;
                }
                else Struct10Setup(s10);
            }
            return result;
        }

        public void AdlibSetParam(int channel, int param, int value)
        {
            int reg;
            if (param <= 12) reg = Operator2Offsets[channel];
            else if (param <= 25) { param -= 13; reg = Operator1Offsets[channel]; }
            else if (param <= 27) { param -= 13; reg = channel; }
            else if (param is 28 or 29)
            {
                value -= param == 28 ? 15 : 383;
                value *= 16;
                _channelTable2[channel] = (ushort)value;
                AdlibPlayNote(channel, _curNotTable[channel] + value);
                return;
            }
            else return;

            var set = SetParams[param];
            if (set[3] != 0) value = set[3] - value;
            reg += set[0];
            AdlibWrite(reg, Reg(reg) & ~set[2] | ((byte)value << set[1]));
        }

        private void AdlibKeyOnOff(int channel)
        {
            var reg = channel + 0xB0;
            var val = Reg(reg);
            AdlibWrite(reg, val & ~0x20);
            AdlibWrite(reg, val | 0x20);
        }

        private void Struct10Setup(Struct10 s10)
        {
            int b = Math.Clamp((int)s10.Unk3, 0, 31); // a negative table index would read outside the table
            var f = s10.Active - 1;
            var t = s10.TableA[f];
            int e = NumStepsTable[VolumeLookup[t & 0x7F, b]];
            if ((t & 0x80) != 0) e = RandomNr(e);
            if (e == 0) e++;
            s10.NumSteps = s10.SpeedLoMax = (ushort)e;

            int h;
            if (f != 2)
            {
                int c = s10.MaxValue;
                int g = s10.StartValue;
                t = s10.TableB[f];
                var d = LookupVolume(c, (t & 0x7F) - 31);
                if ((t & 0x80) != 0) d = RandomNr(d);
                if (d + g > c) h = c - g;
                else
                {
                    h = d;
                    if (d + g < 0) h = -g;
                }
                h -= s10.CurVal;
            }
            else h = 0;

            s10.SpeedHi = (short)(h / e);
            if (h < 0)
            {
                h = -h;
                s10.Direction = -1;
            }
            else s10.Direction = 1;
            s10.SpeedLo = (ushort)(h % e);
            s10.SpeedLoCounter = 0;
        }

        private void AdlibPlayNote(int channel, int note)
        {
            var note2 = (byte)((note >> 7) - 4);
            if (note2 >= 128) note2 = 0;
            int oct = note2 / 12;
            oct = oct > 7 ? 7 << 2 : oct << 2;
            var notex = note2 % 12 + 3;
            int old = Reg(channel + 0xB0);
            if ((old & 0x20) != 0)
            {
                old &= ~0x20;
                if (oct > old)
                {
                    if (notex < 6) { notex += 12; oct -= 4; }
                }
                else if (oct < old)
                {
                    if (notex > 11) { notex -= 12; oct += 4; }
                }
            }
            var i = Math.Clamp((notex << 3) + (note >> 4 & 0x7), 0, NoteFrequencies.Length - 1);
            AdlibWrite(channel + 0xA0, NoteFrequencies[i]);
            AdlibWrite(channel + 0xB0, (byte)oct | 0x20);
        }

        private int RandomNr(int a)
        {
            if ((_randSeed & 1) != 0)
            {
                _randSeed >>= 1;
                _randSeed ^= 0xB8;
            }
            else _randSeed >>= 1;
            return _randSeed * a >> 8;
        }

        public void PartKeyOff(Part part, byte note)
        {
            for (var voice = part.Voice; voice is not null;)
            {
                var next = voice.Next;
                if (voice.Note == note)
                {
                    if (part.Pedal) voice.WaitForPedal = true;
                    else McOff(voice);
                }
                voice = next;
            }
        }

        public void PartKeyOn(Part part, byte[] instrument, byte note, byte velocity)
        {
            var voice = AllocateVoice(part.PriEff);
            if (voice is null) return;
            LinkMc(part, voice);
            McKeyOn(voice, instrument, note, velocity);
        }

        private Voice? AllocateVoice(byte pri)
        {
            Voice? best = null;
            for (var i = 0; i < 9; i++)
            {
                if (++_voiceIndex >= 9) _voiceIndex = 0;
                var ac = _voices[_voiceIndex];
                if (ac.Part is null) return ac;
                if (ac.Next is null && ac.Part.PriEff <= pri)
                {
                    pri = ac.Part.PriEff;
                    best = ac;
                }
            }
            if (best is not null) McOff(best);
            return best;
        }

        private static void LinkMc(Part part, Voice voice)
        {
            voice.Part = part;
            voice.Next = part.Voice;
            part.Voice = voice;
            voice.Prev = null;
            if (voice.Next is not null) voice.Next.Prev = voice;
        }

        private void McKeyOn(Voice voice, byte[] instr, byte note, byte velocity)
        {
            var part = voice.Part!;
            voice.TwoChan = (instr[10] & 1) != 0;
            voice.Note = note;
            voice.WaitForPedal = false;
            voice.Duration = instr[29];
            if (voice.Duration != 0) voice.Duration = (short)(voice.Duration * 63);

            var vol1 = (instr[1] & 0x3F) + VolumeLookup[velocity >> 1, instr[4] >> 2];
            if (vol1 > 0x3F) vol1 = 0x3F;
            voice.Vol1 = (byte)vol1;
            var vol2 = (instr[6] & 0x3F) + VolumeLookup[velocity >> 1, instr[9] >> 2];
            if (vol2 > 0x3F) vol2 = 0x3F;
            voice.Vol2 = (byte)vol2;

            var c = part.VolEff >> 2;
            vol2 = VolumeTable[VolumeLookup[vol2, c]];
            if (voice.TwoChan) vol1 = VolumeTable[VolumeLookup[vol1, c]];

            AdlibSetupChannel(voice.Channel, instr, vol1, vol2);
            AdlibNoteOnEx(voice.Channel, note + part.TransposeEff, part.DetuneEff + (part.PitchBendValue * part.PitchBendFactorValue >> 6));

            if ((instr[11] & 0x80) != 0) McInitStuff(voice, voice.S10a, voice.S11a, instr[11], instr, 12);
            else voice.S10a.Active = 0;
            if ((instr[20] & 0x80) != 0) McInitStuff(voice, voice.S10b, voice.S11b, instr[20], instr, 21);
            else voice.S10b.Active = 0;
        }

        private void AdlibSetupChannel(int chan, byte[] instr, int vol1, int vol2)
        {
            int channel = Operator1Offsets[chan];
            AdlibWrite(channel + 0x20, instr[0]);
            AdlibWrite(channel + 0x40, (instr[1] | 0x3F) - vol1);
            AdlibWrite(channel + 0x60, 0xFF & ~instr[2]);
            AdlibWrite(channel + 0x80, 0xFF & ~instr[3]);
            AdlibWrite(channel + 0xE0, instr[4]);

            channel = Operator2Offsets[chan];
            AdlibWrite(channel + 0x20, instr[5]);
            AdlibWrite(channel + 0x40, (instr[6] | 0x3F) - vol2);
            AdlibWrite(channel + 0x60, 0xFF & ~instr[7]);
            AdlibWrite(channel + 0x80, 0xFF & ~instr[8]);
            AdlibWrite(channel + 0xE0, instr[9]);

            AdlibWrite(chan + 0xC0, instr[10]);
        }

        private void McInitStuff(Voice voice, Struct10 s10, Struct11 s11, byte flags, byte[] ie, int extra)
        {
            var part = voice.Part!;
            s11.ModifyVal = 0;
            s11.Flag0x40 = (byte)(flags & 0x40);
            s10.Loop = (byte)(flags & 0x20);
            s11.Flag0x10 = (byte)(flags & 0x10);
            s11.Param = ParamTable1[flags & 0xF];
            s10.MaxValue = MaxValTable[flags & 0xF];
            s10.Unk3 = 31;
            s10.ModWheel = s11.Flag0x40 != 0 ? (sbyte)(part.ModWheel >> 2) : (sbyte)31;

            switch (s11.Param)
            {
                case 0: s10.StartValue = voice.Vol2; break;
                case 13: s10.StartValue = voice.Vol1; break;
                case 30:
                    s10.StartValue = 31;
                    s11.S10.ModWheel = 0;
                    break;
                case 31:
                    s10.StartValue = 0;
                    s11.S10.Unk3 = 0;
                    break;
                default: s10.StartValue = (short)AdlibGetRegValueParam(voice.Channel, s11.Param); break;
            }
            Struct10Init(s10, ie, extra);
        }

        private void Struct10Init(Struct10 s10, byte[] ie, int extra)
        {
            s10.Active = 1;
            s10.CurVal = 0;
            s10.ModWheelLast = 31;
            s10.Count = ie[extra];
            if (s10.Count != 0) s10.Count = (short)(s10.Count * 63);
            s10.TableA[0] = ie[extra + 1];
            s10.TableA[1] = ie[extra + 3];
            s10.TableA[2] = ie[extra + 5];
            s10.TableA[3] = ie[extra + 6];
            s10.TableB[0] = ie[extra + 2];
            s10.TableB[1] = ie[extra + 4];
            s10.TableB[2] = 0;
            s10.TableB[3] = ie[extra + 7];
            Struct10Setup(s10);
        }

        private int AdlibGetRegValueParam(int chan, int param)
        {
            int channel;
            if (param <= 12) channel = Operator2Offsets[chan];
            else if (param <= 25) { param -= 13; channel = Operator1Offsets[chan]; }
            else if (param <= 27) { param -= 13; channel = chan; }
            else if (param == 28) return 0xF;
            else if (param == 29) return 0x17F;
            else return 0;

            var set = SetParams[param];
            int val = Reg(channel + set[0]);
            val &= set[2];
            val >>= set[1];
            if (set[3] != 0) val = set[3] - val;
            return val;
        }

        public void AdlibNoteOn(int chan, int note, int mod)
        {
            var code = ((byte)note << 7) + mod;
            _curNotTable[chan] = (ushort)code;
            AdlibPlayNote(chan, (short)_channelTable2[chan] + code);
        }

        private void AdlibNoteOnEx(int chan, int note, int mod)
        {
            var code = ((byte)note << 7) + mod;
            _curNotTable[chan] = (ushort)code;
            _channelTable2[chan] = 0;
            AdlibPlayNote(chan, code);
        }
    }
}

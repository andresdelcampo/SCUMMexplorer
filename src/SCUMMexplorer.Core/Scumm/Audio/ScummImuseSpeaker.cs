using System.Buffers.Binary;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// Plays the <c>SPK </c> part of a v5 sound (<see cref="ScummImuseSong"/>) on the PC speaker: ScummVM's iMUSE PC speaker driver
/// (<c>engines/scumm/imuse/drivers/pcspk.cpp</c>) ported as it is. Six channels share the one speaker, of which the loudest-priority active one sounds; a note's
/// pitch comes from a frequency table, its tremolo/vibrato from a 1024-byte wave table and two envelope effects per instrument (23 bytes, the 0x7D 16 exclusive).
/// The driver's timer is 250 Hz, which also quantizes the song's events as the AdLib port does. The iMUSE player half (part set-up, controllers, unconditional
/// hooks, the loop point) is the one of <see cref="ScummImuseAdlib"/>.
/// </summary>
public sealed class ScummImuseSpeaker
{
    private const int SampleRate = 44100;
    private const int CallbackRate = 250;
    private const double ReleaseSeconds = 1;
    private const double MaxSeconds = 420;
    private const double Amplitude = 6000;

    public sealed record Result(byte[] Wave, bool Loops, double Seconds);

    public byte[] RenderWave(byte[] part, CancellationToken cancellationToken = default) => Render(ScummImuseSong.Parse(part), cancellationToken).Wave;

    public Result Render(ScummImuseSong song, CancellationToken cancellationToken = default)
    {
        var driver = new Driver();
        var player = new Player(driver);
        var samples = new List<short>();
        var index = 0;
        long shift = 0, skipUntil = 0;
        var loops = false;
        double? end = null;
        var events = song.Events;
        var maxSteps = (int)(MaxSeconds * CallbackRate);
        double phase = 0;
        var frameRemainder = 0;

        for (var step = 0; step < maxSteps; step++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var now = step / (double)CallbackRate;
            while (end is null && index < events.Count && song.SecondsAt(events[index].Tick - shift) <= now + 1e-9)
            {
                var e = events[index++];
                if (e.Tick < skipUntil) continue;
                if (player.Handle(e) is not { } target) continue;
                if (target <= e.Tick) { loops = true; end = song.SecondsAt(e.Tick - shift); }
                else { shift += target - e.Tick; skipUntil = target; }
            }
            if (end is null && index >= events.Count) end = song.SecondsAt(song.Ticks - shift);
            if (end is { } stop && now >= stop + ReleaseSeconds) break;
            driver.OnTimer();
            frameRemainder += SampleRate;
            var frames = frameRemainder / CallbackRate;
            frameRemainder %= CallbackRate;
            // A square wave at the speaker's current frequency; each sample is the mean of the wave over its interval (no aliasing).
            var hz = (double)driver.Frequency;
            for (var i = 0; i < frames; i++)
            {
                if (hz <= 0) { samples.Add(0); phase = 0; continue; }
                var advance = hz / SampleRate;
                var next = phase + advance;
                // High part of the cycle is [0, 0.5), low part [0.5, 1).
                double Area(double x) => Math.Floor(x) * 0.5 + Math.Min(x - Math.Floor(x), 0.5);
                var high = Area(next) - Area(phase);
                var mean = (2 * high / advance) - 1;
                samples.Add((short)Math.Round(mean * Amplitude));
                phase = next - Math.Floor(next);
            }
        }
        var last = samples.Count - 1;
        while (last > 0 && samples[last] == 0) last--;
        if (last + 1 < samples.Count) samples.RemoveRange(last + 1, samples.Count - last - 1);
        if (samples.Count == 0) throw new InvalidDataException("The PC speaker music produced no audio.");
        return new Result(ToWave(samples), loops, samples.Count / (double)SampleRate);
    }

    private static byte[] ToWave(List<short> samples)
    {
        var wave = new byte[44 + samples.Count * 2];
        "RIFF"u8.CopyTo(wave);
        BinaryPrimitives.WriteInt32LittleEndian(wave.AsSpan(4), 36 + samples.Count * 2);
        "WAVEfmt "u8.CopyTo(wave.AsSpan(8));
        BinaryPrimitives.WriteInt32LittleEndian(wave.AsSpan(16), 16);
        BinaryPrimitives.WriteInt16LittleEndian(wave.AsSpan(20), 1);
        BinaryPrimitives.WriteInt16LittleEndian(wave.AsSpan(22), 1);
        BinaryPrimitives.WriteInt32LittleEndian(wave.AsSpan(24), SampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(wave.AsSpan(28), SampleRate * 2);
        BinaryPrimitives.WriteInt16LittleEndian(wave.AsSpan(32), 2);
        BinaryPrimitives.WriteInt16LittleEndian(wave.AsSpan(34), 16);
        "data"u8.CopyTo(wave.AsSpan(36));
        BinaryPrimitives.WriteInt32LittleEndian(wave.AsSpan(40), samples.Count * 2);
        for (var i = 0; i < samples.Count; i++) BinaryPrimitives.WriteInt16LittleEndian(wave.AsSpan(44 + i * 2), samples[i]);
        return wave;
    }

    // ---------------------------------------------------------------- iMUSE player half

    private sealed class PartState(Channel mc)
    {
        public Channel Mc { get; } = mc;
        public bool On, InstrumentValid;
        public sbyte Pri, Detune;
        public byte Vol;
        public int Transpose;
    }

    private sealed class Player(Driver driver)
    {
        private readonly PartState?[] _parts = new PartState?[16];
        private readonly byte[]?[] _globalInstruments = new byte[]?[32];

        /// <summary>Part::load_global_instrument / copyGlobalInstrument: the global instrument of a slot, or the all-zero default the original uses.</summary>
        private static void LoadInstrument(PartState state, byte[]? global)
        {
            Array.Clear(state.Mc.Instrument);
            global?.CopyTo(state.Mc.Instrument, 0);
            state.InstrumentValid = true;
        }

        private PartState? Part(int chan)
        {
            if (_parts[chan] is { } existing) return existing;
            var mc = driver.AllocateChannel();
            return mc is null ? null : _parts[chan] = new PartState(mc);
        }

        public long? Handle(ScummImuseEvent e)
        {
            if (e.IsSysex) return Sysex(e.Data);
            if (_parts[e.Channel] is not { } state) return null;
            var mc = state.Mc;
            switch (e.Command)
            {
                case 0x8: mc.NoteOff(e.Data[0]); break;
                case 0x9:
                    if (e.Data[1] == 0) mc.NoteOff(e.Data[0]);
                    else if (state.On && state.InstrumentValid) mc.NoteOn(e.Data[0], e.Data[1]);
                    break;
                case 0xB:
                    switch (e.Data[0])
                    {
                        case 1: mc.ControlChange(1, e.Data[1]); break;
                        case 7: state.Vol = e.Data[1]; mc.ControlChange(7, EffectiveVolume(e.Data[1])); break;
                        case 16: if (e.Data[1] <= 12) { mc.PitchBend(0); mc.PitchBendFactor(e.Data[1]); } break;
                        case 17: state.Detune = (sbyte)(e.Data[1] - 0x40); mc.Detune(state.Detune); break;
                        case 18: state.Pri = (sbyte)(e.Data[1] - 0x40); mc.SetPriority((byte)Math.Clamp((int)state.Pri, 0, 255)); break;
                        case 64: mc.ControlChange(64, e.Data[1]); break;
                        case 123: mc.ControlChange(123, 0); break;
                    }
                    break;
                case 0xC: if (e.Data[0] < 32) LoadInstrument(state, _globalInstruments[e.Data[0]]); break;
                case 0xE: mc.PitchBend((e.Data[0] | e.Data[1] << 7) - 0x2000); break;
            }
            return null;
        }

        private static int EffectiveVolume(int volume) => (volume + 1) * 127 >> 7;

        private long? Sysex(byte[] d)
        {
            if (d.Length < 2 || d[0] != 0x7D) return null;
            switch (d[1])
            {
                case 0:
                {
                    if (d.Length < 3) break;
                    var chan = d[2] & 0x0F;
                    var buf = ScummImuseSong.Decode(d.AsSpan(3), 8).Prepend((byte)chan).ToArray();
                    if (buf.Length < 9) break;
                    var state = Part(chan);
                    if (state is null) break;
                    var mc = state.Mc;
                    var on = (buf[1] & 1) != 0;
                    if (state.On != on)
                    {
                        state.On = on;
                        if (!on) { mc.ControlChange(64, 0); mc.ControlChange(123, 0); }
                    }
                    state.Pri = (sbyte)buf[2];
                    state.Vol = buf[3];
                    state.Transpose = buf[5] == 0x80 ? 0 : Math.Clamp((sbyte)buf[5], (sbyte)-12, (sbyte)12);
                    state.Detune = (sbyte)buf[6];
                    if (buf[8] < 32) LoadInstrument(state, _globalInstruments[buf[8]]);
                    // Part::sendAll
                    if (buf[7] <= 12) { mc.PitchBend(0); mc.PitchBendFactor(buf[7]); }
                    mc.Transpose((sbyte)state.Transpose);
                    mc.Detune(state.Detune);
                    mc.PitchBend(0);
                    mc.ControlChange(7, EffectiveVolume(state.Vol));
                    mc.SetPriority((byte)Math.Clamp((int)state.Pri, 0, 255));
                    break;
                }
                case 1:
                    if (d.Length > 2 && _parts[d[2] & 0x0F] is { } off)
                    {
                        off.On = false;
                        off.Mc.ControlChange(64, 0);
                        off.Mc.ControlChange(123, 0);
                        off.Mc.Release();
                        _parts[d[2] & 0x0F] = null;
                    }
                    break;
                case 16: // instrument definition: 23 bytes for the PC speaker
                {
                    if (d.Length < 4) break;
                    var state = Part(d[2] & 0x0F);
                    if (state is null) break;
                    if (d.Length == 50)
                    {
                        var raw = ScummImuseSong.Decode(d.AsSpan(4), 23);
                        raw.CopyTo(state.Mc.Instrument, 0);
                        state.InstrumentValid = true;
                    }
                    break;
                }
                case 17: // instrument definition (global)
                    if (d.Length == 51 && d[4] < 32)
                    {
                        var instrument = new byte[23];
                        ScummImuseSong.Decode(d.AsSpan(5), 23).CopyTo(instrument, 0);
                        _globalInstruments[d[4] & 31] = instrument;
                    }
                    break;
                case 48 when d.Length > 3:
                {
                    var buf = ScummImuseSong.Decode(d.AsSpan(3), 9);
                    if (buf.Length < 7 || buf[0] != 0) break;
                    return (long)(BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(3)) - 1) * 480 + BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(5));
                }
                case 80:
                    return 0;
            }
            return null;
        }
    }

    // ---------------------------------------------------------------- PC speaker driver

    private sealed class Envelope
    {
        public byte State;
        public short CurrentLevel, Duration, MaxLevel, StartLevel;
        public byte Loop;
        public readonly byte[] StateTargetLevels = new byte[4], StateModWheelLevels = new byte[4];
        public byte ModWheelSensitivity, ModWheelState, ModWheelLast;
        public short StateNumSteps, StateStepCounter, ChangePerStep;
        public sbyte Dir;
        public short ChangePerStepRem, ChangeCountRem;
    }

    private sealed class EffectDef
    {
        public short Phase;
        public byte Type, UseModWheel;
    }

    private sealed class Output
    {
        public byte Active, Note, SustainNoteOff, Length;
        public int Instrument = -1; // offset into the wave table, or -1
        public byte UnkA, UnkB, UnkC;
        public short UnkE;
        public Envelope EnvA = new(), EnvB = new();
        public EffectDef DefA = new(), DefB = new();
        public short Unk60;
    }

    private sealed class Channel(Driver owner)
    {
        public bool Allocated;
        public Output Out = new();
        public readonly byte[] Instrument = new byte[23];
        public byte Priority, Tl, ModWheel;
        public short PitchBendValue;
        private byte _sustain, _pitchBendFactor = 2;
        private short _pitchBendTmp;
        private sbyte _transpose, _detune;

        public bool Allocate()
        {
            if (Allocated) return false;
            Out = new Output();
            Array.Clear(Instrument);
            Allocated = true;
            return true;
        }

        public void Release()
        {
            Out.Active = 0;
            Allocated = false;
            owner.UpdateNote();
        }

        public void NoteOff(byte note)
        {
            if (!Allocated) return;
            if (_sustain != 0)
            {
                if (Out.Note == note) Out.SustainNoteOff = 1;
            }
            else if (Out.Note == note)
            {
                Out.Active = 0;
                owner.UpdateNote();
            }
        }

        public void NoteOn(byte note, byte velocity)
        {
            if (!Allocated) return;
            Out.Note = note;
            Out.SustainNoteOff = 0;
            Out.Length = Instrument[0];
            Out.Instrument = Instrument[4] * 256 < ScummImuseSpeakerTables.OutInstrumentData.Length ? Instrument[4] * 256 : -1;
            Out.UnkA = 0;
            Out.UnkB = Instrument[1];
            Out.UnkC = Instrument[2];
            Out.UnkE = 0;
            Out.Unk60 = 0;
            Out.Active = 1;
            if (owner.LastActiveChannel == this)
            {
                owner.LastActiveChannel = null;
                owner.LastActiveOut = 0;
            }
            owner.UpdateNote();
            Out.UnkC = (byte)(Out.UnkC + Driver.GetEffectModifier((ushort)(Instrument[3] + ((velocity & 0xFE) << 4))));
            if (Out.UnkC > 63) Out.UnkC = 63;
            if ((Instrument[5] & 0x80) != 0) owner.SetupEffects(this, Out.EnvA, Out.DefA, Instrument[5], Instrument, 6);
            if ((Instrument[14] & 0x80) != 0) owner.SetupEffects(this, Out.EnvB, Out.DefB, Instrument[14], Instrument, 15);
        }

        public void PitchBend(int bend)
        {
            _pitchBendTmp = (short)bend;
            Recompute();
        }

        private void Recompute() => PitchBendValue = (short)((_transpose << 7) + ((_pitchBendTmp * _pitchBendFactor) >> 6) + _detune);

        public void ControlChange(int control, int value)
        {
            switch (control)
            {
                case 1:
                    if (Out.EnvA.State != 0 && Out.DefA.UseModWheel != 0) Out.EnvA.ModWheelState = (byte)(value >> 2);
                    if (Out.EnvB.State != 0 && Out.DefB.UseModWheel != 0) Out.EnvB.ModWheelState = (byte)(value >> 2);
                    break;
                case 7:
                    Tl = (byte)value;
                    if (owner.ActiveChannel == this)
                    {
                        if (Tl == 0)
                        {
                            owner.LastActiveChannel = null;
                            owner.LastActiveOut = 0;
                            owner.Frequency = 0;
                        }
                        else owner.OutputNote((ushort)((Out.Note << 7) + PitchBendValue + Out.Unk60 + Out.UnkE));
                    }
                    break;
                case 64:
                    _sustain = (byte)value;
                    if (value == 0 && Out.SustainNoteOff != 0)
                    {
                        Out.Active = 0;
                        owner.UpdateNote();
                    }
                    break;
                case 123:
                    Out.Active = 0;
                    owner.UpdateNote();
                    break;
            }
        }

        public void PitchBendFactor(byte value) => _pitchBendFactor = value;

        public void Transpose(sbyte value)
        {
            _transpose = value;
            Recompute();
        }

        public void Detune(short value)
        {
            _detune = (sbyte)value;
            Recompute();
        }

        public void SetPriority(byte value) => Priority = value;
    }

    private sealed class Driver
    {
        private readonly Channel[] _channels = new Channel[6];
        public Channel? ActiveChannel, LastActiveChannel;
        public ushort LastActiveOut;
        public int Frequency; // Hz; 0: the speaker is off
        private int _effectTimer;
        private byte _randBase = 1;

        public Driver()
        {
            for (var i = 0; i < 6; i++) _channels[i] = new Channel(this);
        }

        public Channel? AllocateChannel()
        {
            foreach (var channel in _channels)
                if (channel.Allocate()) return channel;
            return null;
        }

        public void OnTimer()
        {
            if (ActiveChannel is null) return;
            for (var i = 0; i < 6; i++)
            {
                var channel = _channels[i];
                var o = channel.Out;
                if (o.Active == 0) continue;
                if (o.Length == 0 || --o.Length != 0)
                {
                    if (o.UnkB != 0 && o.UnkC != 0)
                    {
                        o.UnkA += o.UnkB;
                        if (o.Instrument >= 0) o.UnkE = (short)(((sbyte)ScummImuseSpeakerTables.OutInstrumentData[o.Instrument + o.UnkA] * o.UnkC) >> 4);
                    }
                    _effectTimer++;
                    if (_effectTimer > 3)
                    {
                        _effectTimer = 0;
                        if (o.EnvA.State != 0) UpdateEffectGenerator(channel, o.EnvA, o.DefA);
                        if (o.EnvB.State != 0) UpdateEffectGenerator(channel, o.EnvB, o.DefB);
                    }
                }
                else
                {
                    o.Active = 0;
                    UpdateNote();
                    return;
                }
            }
            if (ActiveChannel is { } active && active.Tl != 0)
                OutputNote((ushort)((active.Out.Note << 7) + active.PitchBendValue + active.Out.Unk60 + active.Out.UnkE));
            else
            {
                Frequency = 0;
                LastActiveChannel = null;
                LastActiveOut = 0;
            }
        }

        public void UpdateNote()
        {
            byte priority = 0;
            ActiveChannel = null;
            foreach (var channel in _channels)
            {
                if (channel.Allocated && channel.Out.Active != 0 && channel.Priority >= priority)
                {
                    priority = channel.Priority;
                    ActiveChannel = channel;
                }
            }
            if (ActiveChannel is null || ActiveChannel.Tl == 0)
            {
                Frequency = 0;
                LastActiveChannel = null;
                LastActiveOut = 0;
            }
            else OutputNote((ushort)(ActiveChannel.PitchBendValue + (ActiveChannel.Out.Note << 7)));
        }

        public void OutputNote(ushort value)
        {
            var v1 = value >> 7 & 0xFF;
            var v2 = value >> 2 & 0x1E;
            if (v1 >= ScummImuseSpeakerTables.OutputTable1.Length) v1 &= 0x7F; // the original reads beyond its tables here
            var shift = ScummImuseSpeakerTables.OutputTable1[v1];
            var indexBase = ScummImuseSpeakerTables.OutputTable2[v1] << 5;
            var frequency = ScummImuseSpeakerTables.FrequencyTable[Math.Min((indexBase + v2) / 2, ScummImuseSpeakerTables.FrequencyTable.Length - 1)] >> shift;
            if (LastActiveChannel != ActiveChannel || LastActiveOut != value)
            {
                Frequency = frequency == 0 ? 0 : 1193180 / frequency; // the table holds PIT divisors
                LastActiveChannel = ActiveChannel;
                LastActiveOut = value;
            }
        }

        public static byte GetEffectModifier(ushort level)
        {
            var baseValue = (byte)(level / 32);
            var index = (byte)(level % 32);
            if (index == 0) return 0;
            return (byte)(baseValue * (index + 1) >> 5);
        }

        private short GetEffectModLevel(short level, sbyte mod)
        {
            if (mod == 0) return 0;
            if (mod == 31) return level;
            if (level < -63 || level > 63) return (short)(mod * (level + 1) >> 6);
            if (mod < 0)
            {
                return level < 0
                    ? GetEffectModifier((ushort)((-level << 5) - mod))
                    : (short)-GetEffectModifier((ushort)((level << 5) - mod));
            }
            return level < 0
                ? (short)-GetEffectModifier((ushort)((-level << 5) + mod))
                : GetEffectModifier((ushort)((-level << 5) + mod));
        }

        private short GetRandScale(short input)
        {
            if ((_randBase & 1) != 0) _randBase = (byte)(_randBase >> 1 ^ 0xB8);
            else _randBase >>= 1;
            return (short)(_randBase * input >> 8);
        }

        public void SetupEffects(Channel chan, Envelope env, EffectDef def, byte flags, byte[] data, int offset)
        {
            def.Phase = 0;
            def.UseModWheel = (byte)(flags & 0x40);
            env.Loop = (byte)(flags & 0x20);
            def.Type = (byte)(flags & 0x1F);
            env.ModWheelSensitivity = 31;
            env.ModWheelState = def.UseModWheel != 0 ? (byte)(chan.ModWheel >> 2) : (byte)31;
            switch (def.Type)
            {
                case 0: env.MaxLevel = 767; env.StartLevel = 383; break;
                case 1: env.MaxLevel = 31; env.StartLevel = 15; break;
                case 2: env.MaxLevel = 63; env.StartLevel = chan.Out.UnkB; break;
                case 3: env.MaxLevel = 63; env.StartLevel = chan.Out.UnkC; break;
                case 4: env.MaxLevel = 3; env.StartLevel = chan.Instrument[4]; break;
                case 5: env.MaxLevel = 62; env.StartLevel = 31; env.ModWheelState = 0; break;
                case 6: env.MaxLevel = 31; env.StartLevel = 0; env.ModWheelSensitivity = 0; break;
            }
            StartEffect(env, data, offset);
        }

        private void StartEffect(Envelope env, byte[] data, int o)
        {
            env.State = 1;
            env.CurrentLevel = 0;
            env.ModWheelLast = 31;
            env.Duration = (short)(data[o] * 63);
            env.StateTargetLevels[0] = data[o + 1];
            env.StateTargetLevels[1] = data[o + 3];
            env.StateTargetLevels[2] = data[o + 5];
            env.StateTargetLevels[3] = data[o + 6];
            env.StateModWheelLevels[0] = data[o + 2];
            env.StateModWheelLevels[1] = data[o + 4];
            env.StateModWheelLevels[2] = 0;
            env.StateModWheelLevels[3] = data[o + 7];
            InitNextEnvelopeState(env);
        }

        private void InitNextEnvelopeState(Envelope env)
        {
            var lastState = env.State - 1;
            int stepCount = ScummImuseSpeakerTables.EffectEnvStepTable[GetEffectModifier((ushort)(((env.StateTargetLevels[lastState] & 0x7F) << 5) + env.ModWheelSensitivity)) & 31];
            if ((env.StateTargetLevels[lastState] & 0x80) != 0) stepCount = (ushort)GetRandScale((short)stepCount);
            if (stepCount == 0) stepCount = 1;
            env.StateNumSteps = env.StateStepCounter = (short)stepCount;

            int totalChange = 0;
            if (lastState != 2)
            {
                totalChange = GetEffectModLevel(env.MaxLevel, (sbyte)((env.StateModWheelLevels[lastState] & 0x7F) - 31));
                if ((env.StateModWheelLevels[lastState] & 0x80) != 0) totalChange = GetRandScale((short)totalChange);
                if (totalChange + env.StartLevel > env.MaxLevel) totalChange = env.MaxLevel - env.StartLevel;
                else if (totalChange + env.StartLevel < 0) totalChange = -env.StartLevel;
                totalChange -= env.CurrentLevel;
            }
            env.ChangePerStep = (short)(totalChange / stepCount);
            if (totalChange < 0)
            {
                totalChange = -totalChange;
                env.Dir = -1;
            }
            else env.Dir = 1;
            env.ChangePerStepRem = (short)(totalChange % stepCount);
            env.ChangeCountRem = 0;
        }

        private void UpdateEffectGenerator(Channel chan, Envelope env, EffectDef def)
        {
            if ((AdvanceEffectEnvelope(env, def) & 1) == 0) return;
            switch (def.Type)
            {
                case 0 or 1: chan.Out.Unk60 = (short)(def.Phase << 4); break;
                case 2: chan.Out.UnkB = (byte)((def.Phase & 0xFF) + chan.Instrument[1]); break;
                case 3: chan.Out.UnkC = (byte)((def.Phase & 0xFF) + chan.Instrument[2]); break;
                case 4:
                {
                    var offset = (chan.Instrument[4] + (def.Phase & 0xFF)) * 256;
                    chan.Out.Instrument = offset < ScummImuseSpeakerTables.OutInstrumentData.Length ? offset : -1;
                    break;
                }
                case 5: env.ModWheelState = (byte)(def.Phase & 0xFF); break;
                case 6: env.ModWheelSensitivity = (byte)(def.Phase & 0xFF); break;
            }
        }

        private byte AdvanceEffectEnvelope(Envelope env, EffectDef def)
        {
            if (env.Duration != 0)
            {
                env.Duration -= 17;
                if (env.Duration <= 0)
                {
                    env.State = 0;
                    return 0;
                }
            }
            byte changed = 0;
            var newLevel = (short)(env.CurrentLevel + env.ChangePerStep);
            env.ChangeCountRem += env.ChangePerStepRem;
            if (env.ChangeCountRem >= env.StateNumSteps)
            {
                env.ChangeCountRem -= env.StateNumSteps;
                newLevel += env.Dir;
            }
            if (env.CurrentLevel != newLevel || env.ModWheelLast != env.ModWheelState)
            {
                env.CurrentLevel = newLevel;
                env.ModWheelLast = env.ModWheelState;
                var newPhase = GetEffectModLevel(newLevel, (sbyte)env.ModWheelState);
                if (def.Phase != newPhase)
                {
                    changed |= 1;
                    def.Phase = newPhase;
                }
            }
            env.StateStepCounter--;
            if (env.StateStepCounter == 0)
            {
                env.State++;
                if (env.State > 4)
                {
                    if (env.Loop != 0)
                    {
                        env.State = 1;
                        changed |= 2;
                    }
                    else
                    {
                        env.State = 0;
                        return changed;
                    }
                }
                InitNextEnvelopeState(env);
            }
            return changed;
        }
    }
}

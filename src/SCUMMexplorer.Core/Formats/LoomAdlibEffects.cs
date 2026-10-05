// AdLib effect state machine adapted from ScummVM Player_AD (ScummVM developers).
// https://github.com/scummvm/scummvm/blob/master/engines/scumm/players/player_ad.cpp
// GPL-3.0-or-later; see licenses/GPL-3.0.txt. OPL synthesis uses NukedOPL3Sharp.
using NukedOPL3Sharp;

namespace SCUMMexplorer.Core.Formats;

public sealed partial class LoomAdlibAudio
{
    private static byte[] RenderEffectWave(byte[] data, CancellationToken cancellationToken)
    {
        var player = new EffectPlayer(data);
        var samples = new List<short>();
        int remainder = 0, quiet = 0, release = 0;
        for (int callback = 0; callback < CallbackRate * MaxSeconds; callback++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            player.Tick();
            remainder += SampleRate;
            int frames = remainder / CallbackRate; remainder %= CallbackRate;
            var buffer = new short[frames * 2]; player.Chip.GenerateStream(buffer); samples.AddRange(buffer);
            if (!player.Active)
            {
                quiet = buffer.All(sample => Math.Abs((int)sample) <= QuietSampleLevel) ? quiet + 1 : 0;
                if (++release >= CallbackRate * MaxReleaseSeconds || quiet >= CallbackRate / 5) break;
            }
        }
        if (samples.All(sample => sample == 0)) throw new InvalidDataException("AdLib effect produced no audio.");
        // Bound looping effects and lingering envelopes without a discontinuity at the preview's end.
        int fade = Math.Min(samples.Count, SampleRate / 100 * 2);
        for (int i = 0; i < fade; i++) samples[samples.Count - fade + i] = (short)(samples[samples.Count - fade + i] * (fade - 1 - i) / fade);
        return ToWave(samples);
    }

    private sealed class EffectPlayer
    {
        private static readonly int[] Steps = [1, 4, 6, 8, 10, 14, 18, 24, 36, 64, 100, 160, 240, 340, 600, 1200];
        private static readonly int[] Bias = [0, 0, 63, 0, 63, 0, 0];
        private static readonly int[] Scale = [255, 7, 63, 15, 63, 15, 63];
        private static readonly int[] BaseRegister = [0xA0, 0xC0, 0x40, 0x20, 0x40, 0x20, 0];
        private static readonly int[] Masks = [255, 14, 63, 15, 63, 15, 0];
        private readonly byte[] _data;
        private readonly byte[] _registers = new byte[256];
        private readonly List<Channel> _channels = [];
        private int _timer = 4, _random = 1;
        public Opl3Chip Chip { get; } = new();
        public bool Active => _channels.Any(channel => channel.State != 0);
        public bool LoopBoundaryReached { get; private set; }

        public EffectPlayer(byte[] data)
        {
            _data = data; Chip.Reset(SampleRate); Write(1, 0x20); Write(0xBD, 0); Write(8, 0);
            _channels.Add(new Channel(0, 2));
            int position = 2;
            for (int commands = 0; commands < data.Length; commands++)
            {
                int command = Read(position);
                if (command == 255) return;
                if (command == 1) { Require(position, 15); position += 15; }
                else if (command == 2) { Require(position, 11); position += 11; }
                else if (command == 128) position++;
                else
                {
                    if (_channels.Count == 3) throw new InvalidDataException("AdLib effect has more than three channels.");
                    _channels.Add(new Channel(_channels.Count, ++position));
                }
            }
            throw new InvalidDataException("AdLib effect has no end marker.");
        }

        public void Tick()
        {
            if (--_timer != 0) return; _timer = 4;
            foreach (var channel in _channels)
            {
                if (channel.State == 1) Parse(channel);
                else if (channel.State == 2) Animate(channel);
            }
        }

        private void Parse(Channel channel)
        {
            for (int guard = 0; guard < _data.Length; guard++)
            {
                int position = channel.Position, command = Read(position);
                if (command == 1)
                {
                    Require(position, 15); int p = position + 1;
                    int[] offsets = [0, 2, 9, 8, 4, 3];
                    for (int i = 0; i < 6; i++) channel.Instrument[i] = Read(p + offsets[i]);
                    Write(0xC0 + channel.Number, Read(p + 2));
                    for (int op = 0; op < 2; op++)
                    {
                        int o = OperatorOffsets[channel.Number * 2 + op], start = p + 3 + op * 5;
                        int[] bases = [0x20, 0x40, 0x60, 0x80, 0xE0];
                        for (int i = 0; i < 5; i++) Write(bases[i] + o, Read(start + i));
                    }
                    Write(0xA0 + channel.Number, Read(p)); Write(0xB0 + channel.Number, Read(p + 1) & 0xDF);
                    channel.Position += 15;
                }
                else if (command == 2)
                {
                    Require(position, 11); channel.State = 2; Retrigger(channel);
                    for (int i = 0; i < 2; i++)
                    {
                        int p = position + 1 + i * 5, flags = Read(p); var note = channel.Notes[i];
                        if ((flags & 128) == 0) continue;
                        note.State = -1; NextStage(channel, note, p); note.PlayTime = 0;
                        if ((flags & 32) != 0) note.PlayTime = (Read(p + 4) >> 4) * 118 + (Read(p + 4) & 15) * 8;
                    }
                    return;
                }
                else if (command == 128) { LoopBoundaryReached = true; channel.Position = channel.Start; }
                else { Write(0xA0 + channel.Number, 0); Write(0xB0 + channel.Number, 0); channel.State = 0; return; }
            }
            throw new InvalidDataException("AdLib effect loops without a timed note.");
        }

        private void Animate(Channel channel)
        {
            // Capture both descriptors before either envelope changes the instruction pointer.
            int position = channel.Position + 1;
            for (int i = 0; i < 2; i++)
            {
                int p = position + i * 5, flags = Read(p); if ((flags & 128) == 0) continue;
                var note = channel.Notes[i]; bool finished;
                if (note.State == 2) finished = --note.Sustain == 0;
                else
                {
                    if (note.PreIncrease) note.Value = unchecked((byte)(note.Value + note.Adjust));
                    note.Counter += note.Increase;
                    if (note.Counter >= note.Steps) { note.Counter -= note.Steps; note.Value = unchecked((byte)(note.Value + (note.Adjust < 0 ? -1 : 1))); }
                    finished = --note.Timer == 0;
                    SetParameter(channel, flags & 7, note.Bias == 0 ? note.Value : note.Bias - note.Value);
                }
                if (finished && NextStage(channel, note, p))
                {
                    if ((flags & 8) == 0) { channel.Position += 11; channel.State = 1; continue; }
                    // No descriptor can advance this note if every enabled envelope repeats without a play-time.
                    var descriptors = new[] { Read(position), Read(position + 5) }.Where(value => (value & 128) != 0);
                    if (descriptors.All(value => (value & 40) == 8)) LoopBoundaryReached = true;
                    if ((flags & 16) != 0) Retrigger(channel);
                    note.State = -1; NextStage(channel, note, p);
                }
                if ((flags & 32) != 0 && --note.PlayTime == 0) { channel.Position += 11; channel.State = 1; }
            }
        }

        private bool NextStage(Channel channel, Note note, int position)
        {
            if (++note.State == 4) return true;
            int parameter = Read(position) & 7;
            if (parameter >= 7) throw new InvalidDataException("Invalid AdLib effect parameter.");
            note.Bias = Bias[parameter];
            int initial = note.State == 0 ? channel.Instrument[parameter] : 0;
            int value = GetParameter(channel, parameter, initial);
            if (note.Bias != 0) value = note.Bias - value;
            note.Value = unchecked((byte)value);
            if (note.State == 2)
            {
                note.Sustain = Steps[Read(position + 3) >> 4];
                if ((Read(position) & 64) != 0) { _random = (_random & 1) != 0 ? (_random >> 1) ^ 0xB8 : _random >> 1; note.Sustain = (_random * note.Sustain >> 8) + 1; }
            }
            else
            {
                int packed = Read(position + (note.State == 3 ? 3 : note.State + 1));
                int steps = Steps[note.State == 3 ? packed & 15 : packed >> 4];
                int target = note.State == 3 ? 0 : ((packed & 15) * 4369 * Scale[parameter] >> 16);
                int change = target - note.Value;
                note.PreIncrease = Math.Abs(change) > steps;
                note.Adjust = note.PreIncrease ? change / steps : change;
                note.Increase = note.PreIncrease ? Math.Abs(change % steps) : Math.Abs(change);
                note.Steps = note.Timer = steps; note.Counter = 0;
            }
            return false;
        }

        private int Register(Channel channel, int parameter) => BaseRegister[parameter] + (parameter is >= 2 and <= 5
            ? OperatorOffsets[channel.Number * 2 + (parameter <= 3 ? 1 : 0)] : channel.Number);
        private int GetParameter(Channel channel, int parameter, int initial)
        {
            if (parameter == 6) return 0;
            int value = initial != 0 ? initial : _registers[Register(channel, parameter)];
            return (value & Masks[parameter]) >> (parameter == 1 ? 1 : 0);
        }
        private void SetParameter(Channel channel, int parameter, int value)
        {
            if (parameter == 6) return;
            int register = Register(channel, parameter);
            Write(register, (_registers[register] & ~Masks[parameter]) | (unchecked((byte)value) << (parameter == 1 ? 1 : 0)));
        }
        private void Retrigger(Channel channel) { int r = 0xB0 + channel.Number, v = _registers[r]; Write(r, v & 0xDF); Write(r, v | 32); }
        private void Write(int register, int value) { _registers[register] = unchecked((byte)value); Chip.WriteRegister((ushort)register, ChipValue(register, unchecked((byte)value))); }
        private int Read(int position) => position >= 0 && position < _data.Length ? _data[position] : throw new InvalidDataException("AdLib effect extends past its resource.");
        private void Require(int position, int length) { if (position < 0 || position > _data.Length - length) throw new InvalidDataException("Truncated AdLib effect command."); }
        private sealed class Channel(int number, int start)
        { public int Number = number, Start = start, Position = start, State = 1; public int[] Instrument = new int[7]; public Note[] Notes = [new(), new()]; }
        private sealed class Note
        { public int State, Value, Bias, PlayTime, Sustain, Adjust, Increase, Steps, Counter, Timer; public bool PreIncrease; }
    }
}

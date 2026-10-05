// YM3812 (OPL2) emulation ported from ScummVM's MAME OPL core (audio/softsynth/opl/mame.cpp), which is
// "LGPL licensed version of MAMEs fmopl (V0.37a modified)" by Tatsuyuki Satoh and others.
// https://github.com/scummvm/scummvm/blob/master/audio/softsynth/opl/mame.cpp
// Copyright (C) ScummVM contributors. SPDX-License-Identifier: GPL-3.0-or-later
//
// ScummVM plays AdLib through this core by default (opl_driver=auto), so LOOM's AdLib music is meant to be heard
// through it. The structure, tables and integer arithmetic follow the C++ line by line (high quality envelope:
// 16 envelope bits, 4096 entries; 2048-entry sine table; clock 3579545 Hz) so the output can be compared sample
// for sample with the original. The chip runs at the rate it is given (the callers use the OPL's native 49716 Hz).
namespace SCUMMexplorer.Core.Formats;

/// <summary>A YM3812 as ScummVM's MAME OPL core emulates it. Mono output.</summary>
public sealed class MameOpl2
{
    private const int EnvBits = 16;
    private const int EgEnt = 4096;
    private const int EgOff = (2 * EgEnt) << EnvBits;
    private const int EgDed = EgOff;
    private const int EgDst = EgEnt << EnvBits;
    private const int EgAed = EgDst;
    private const int EgAst = 0;
    private const double EgStep = 96.0 / EgEnt;
    private const int FreqBits = 24;
    private const int FreqRate = 1 << (FreqBits - 20);
    private const int TlBits = FreqBits + 2;
    private const int OutShift = TlBits + 3 - 16;
    private const int MaxOut = 0x7fff << OutShift;
    private const int MinOut = -(0x8000 << OutShift);
    private const int SinEntShift = 11;
    private const int SinEnt = 1 << SinEntShift;
    private const int VibEnt = 512;
    private const int VibShift = 32 - 9;
    private const int AmsEnt = 512;
    private const int AmsShift = 32 - 9;
    private const int VibRateShift = 8;
    private const int VibRate = 1 << VibRateShift;
    private const int TlMax = EgEnt * 2;
    private const int ArRate = 141280;
    private const int DrRate = 1956000;
    private const int Clock = 3579545;
    private const int EnvModRr = 0, EnvModDr = 1, EnvModAr = 2;

    // ---- shared tables (OPLOpenTable, OPLBuildTables) ----
    private static readonly int[] SlotArray =
    [
        0, 2, 4, 1, 3, 5, -1, -1,
        6, 8, 10, 7, 9, 11, -1, -1,
        12, 14, 16, 13, 15, 17, -1, -1,
        -1, -1, -1, -1, -1, -1, -1, -1
    ];

    private static readonly double[] KslSeed =
    [
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0.750, 1.125, 1.500, 1.875, 2.250, 2.625, 3.000,
        0, 0, 0, 0, 0, 1.125, 1.875, 2.625, 3.000, 3.750, 4.125, 4.500, 4.875, 5.250, 5.625, 6.000,
        0, 0, 0, 1.875, 3.000, 4.125, 4.875, 5.625, 6.000, 6.750, 7.125, 7.500, 7.875, 8.250, 8.625, 9.000,
        0, 0, 3.000, 4.875, 6.000, 7.125, 7.875, 8.625, 9.000, 9.750, 10.125, 10.500, 10.875, 11.250, 11.625, 12.000,
        0, 3.000, 6.000, 7.875, 9.000, 10.125, 10.875, 11.625, 12.000, 12.750, 13.125, 13.500, 13.875, 14.250, 14.625, 15.000,
        0, 6.000, 9.000, 10.875, 12.000, 13.125, 13.875, 14.625, 15.000, 15.750, 16.125, 16.500, 16.875, 17.250, 17.625, 18.000,
        0, 9.000, 12.000, 13.875, 15.000, 16.125, 16.875, 17.625, 18.000, 18.750, 19.125, 19.500, 19.875, 20.250, 20.625, 21.000
    ];

    private static readonly uint[] MulTable =
        [1, 2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 20, 24, 24, 30, 30]; // ML(x) = x * 2

    private static readonly int[] Rate0 = new int[16];
    private static readonly uint[] KslTable = new uint[8 * 16];
    private static readonly int[] SlTable = new int[16];
    private static readonly int[] TlTable = new int[TlMax * 2];
    private static readonly int[] SinTable = new int[SinEnt * 4]; // offsets into TlTable
    private static readonly int[] AmsTable = new int[AmsEnt * 2];
    private static readonly int[] VibTable = new int[VibEnt * 2];
    private static readonly int[] EnvCurve = new int[2 * EgEnt + 1];

    static MameOpl2()
    {
        for (var i = 0; i < KslSeed.Length; i++) KslTable[i] = (uint)(KslSeed[i] / (EgStep / 2));
        double[] slSeed = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 31];
        for (var i = 0; i < 16; i++) SlTable[i] = (int)(slSeed[i] * ((3 / EgStep) * (1 << EnvBits))) + EgDst;

        // total level table
        for (var t = 0; t < EgEnt - 1; t++)
        {
            var rate = ((1 << TlBits) - 1) / Math.Pow(10.0, EgStep * t / 20);
            TlTable[t] = (int)rate;
            TlTable[TlMax + t] = -TlTable[t];
        }
        for (var t = EgEnt - 1; t < TlMax; t++) TlTable[t] = TlTable[TlMax + t] = 0;

        // sine wave table (total level offset); degree 0 = degree 180 = off
        SinTable[0] = SinTable[SinEnt / 2] = EgEnt - 1;
        for (var s = 1; s <= SinEnt / 4; s++)
        {
            var pom = Math.Sin(2 * Math.PI * s / SinEnt);
            pom = 20 * Math.Log10(1 / pom);
            var j = (int)(pom / EgStep);
            SinTable[s] = SinTable[SinEnt / 2 - s] = j;
            SinTable[SinEnt / 2 + s] = SinTable[SinEnt - s] = TlMax + j;
        }
        for (var s = 0; s < SinEnt; s++)
        {
            SinTable[SinEnt * 1 + s] = s < SinEnt / 2 ? SinTable[s] : EgEnt;
            SinTable[SinEnt * 2 + s] = SinTable[s % (SinEnt / 2)];
            SinTable[SinEnt * 3 + s] = (s / (SinEnt / 4)) % 2 == 1 ? EgEnt : SinTable[SinEnt * 2 + s];
        }

        // envelope counter -> envelope output table
        for (var i = 0; i < EgEnt; i++)
        {
            var pom = Math.Pow((double)(EgEnt - 1 - i) / EgEnt, 8) * EgEnt;
            EnvCurve[i] = (int)pom;
            EnvCurve[(EgDst >> EnvBits) + i] = i;
        }
        EnvCurve[EgOff >> EnvBits] = EgEnt - 1;

        // LFO tables
        for (var i = 0; i < AmsEnt; i++)
        {
            var pom = (1.0 + Math.Sin(2 * Math.PI * i / AmsEnt)) / 2;
            AmsTable[i] = (int)((1.0 / EgStep) * pom);
            AmsTable[AmsEnt + i] = (int)((4.8 / EgStep) * pom);
        }
        for (var i = 0; i < VibEnt; i++)
        {
            var pom = (double)VibRate * 0.06 * Math.Sin(2 * Math.PI * i / VibEnt);
            VibTable[i] = (int)(VibRate + (pom * 0.07));
            VibTable[VibEnt + i] = (int)(VibRate + (pom * 0.14));
        }
    }

    private sealed class Slot
    {
        public int Tl, Tll;
        public byte Ksr;       // key scale rate shift
        public int[] Ar = Rate0, Dr = Rate0, Rr = Rate0;
        public int ArOff, DrOff, RrOff;
        public int Sl;
        public byte KslShift;
        public byte KsrValue;
        public uint Mul;
        public uint Cnt, Incr;
        public byte EgTyp, Evm;
        public int Evc, Eve, Evs, Evsa, Evsd, Evsr;
        public byte Ams, Vib;
        public int WaveBase;   // row of SinTable
    }

    private sealed class Channel
    {
        public readonly Slot[] Slots = [new(), new()];
        public byte Con, Fb;
        public readonly int[] Op1Out = new int[2];
        public uint BlockFnum;
        public byte Kcode;
        public uint Fc, KslBase;
        public byte KeyOn;
    }

    private readonly Channel[] _channels = Enumerable.Range(0, 9).Select(_ => new Channel()).ToArray();
    private readonly int _rate;
    private readonly double _freqBase;
    private readonly int[] _arTable = new int[76];
    private readonly int[] _drTable = new int[76];
    private readonly uint[] _fnTable = new uint[1024];
    private readonly int _amsIncr, _vibIncr;
    private uint _amsCnt, _vibCnt;
    private byte _rhythm, _waveSel;
    private int _amsTableBase, _vibTableBase;
    private bool _lfoCaptured;
    private int _amsBase, _vibBase;       // LFO tables as captured when the chip first generated
    private int _outd, _feedback2, _ams, _vib;
    private uint _noise = 0x1234;

    /// <summary>OPLCreate + OPLResetChip: a chip running at <paramref name="rate"/> Hz.</summary>
    public MameOpl2(int rate)
    {
        _rate = rate;
        _freqBase = _rate != 0 ? ((double)Clock / _rate) / 72 : 0;
        // time tables (init_timetables)
        for (var i = 0; i < 4; i++) _arTable[i] = _drTable[i] = 0;
        for (var i = 4; i <= 60; i++)
        {
            var r = _freqBase;
            if (i < 60) r *= 1.0 + (i & 3) * 0.25;
            r *= 1 << ((i >> 2) - 1);
            r *= (double)(EgEnt << EnvBits);
            _arTable[i] = (int)(r / ArRate);
            _drTable[i] = (int)(r / DrRate);
        }
        for (var i = 60; i < 76; i++)
        {
            _arTable[i] = EgAed - 1;
            _drTable[i] = _drTable[60];
        }
        for (var fn = 0; fn < 1024; fn++) _fnTable[fn] = (uint)(_freqBase * fn * FreqRate * (1 << 7) / 2);
        _amsIncr = (int)(_rate != 0 ? (double)AmsEnt * (1 << AmsShift) / _rate * 3.7 * ((double)Clock / 3600000) : 0);
        _vibIncr = (int)(_rate != 0 ? (double)VibEnt * (1 << VibShift) / _rate * 6.4 * ((double)Clock / 3600000) : 0);
        ResetChip();
    }

    /// <summary>OPLWriteReg.</summary>
    public void WriteRegister(ushort register, byte value) => WriteReg(register, value);

    /// <summary>Fills an interleaved stereo buffer with the mono output (left = right).</summary>
    public void GenerateStream(short[] stereo)
    {
        var frames = stereo.Length / 2;
        var mono = new short[frames];
        UpdateOne(mono, frames);
        for (var i = 0; i < frames; i++) stereo[2 * i] = stereo[2 * i + 1] = mono[i];
    }

    private void ResetChip()
    {
        WriteReg(0x01, 0);
        WriteReg(0x02, 0);
        WriteReg(0x03, 0);
        WriteReg(0x04, 0);
        for (var i = 0xff; i >= 0x20; i--) WriteReg(i, 0);
        foreach (var channel in _channels)
            foreach (var slot in channel.Slots)
            {
                slot.WaveBase = 0;
                slot.Evc = EgOff;
                slot.Eve = EgOff + 1;
                slot.Evs = 0;
            }
    }

    // ---- envelope and key handling ----

    private static void KeyOn(Slot slot)
    {
        slot.Cnt = 0;
        slot.Evm = EnvModAr;
        slot.Evs = slot.Evsa;
        slot.Evc = EgAst;
        slot.Eve = EgAed;
    }

    private static void KeyOff(Slot slot)
    {
        if (slot.Evm > EnvModRr)
        {
            // ScummVM's workaround for a pathological note-off straight after note-on.
            if (slot.Evm == EnvModAr && slot.Evc == EgAst) slot.Evc = EgDed;
            else if ((slot.Evc & EgDst) == 0) slot.Evc = EgDst;
            slot.Eve = EgDed;
            slot.Evs = slot.Evsr;
            slot.Evm = EnvModRr;
        }
    }

    private uint CalcSlot(Slot slot)
    {
        if ((slot.Evc += slot.Evs) >= slot.Eve)
        {
            switch (slot.Evm)
            {
                case EnvModAr:
                    slot.Evm = EnvModDr;
                    slot.Evc = EgDst;
                    slot.Eve = slot.Sl;
                    slot.Evs = slot.Evsd;
                    break;
                case EnvModDr:
                    slot.Evc = slot.Sl;
                    slot.Eve = EgDed;
                    if (slot.EgTyp != 0) slot.Evs = 0;
                    else
                    {
                        slot.Evm = EnvModRr;
                        slot.Evs = slot.Evsr;
                    }
                    break;
                case EnvModRr:
                    slot.Evc = EgOff;
                    slot.Eve = EgOff + 1;
                    slot.Evs = 0;
                    break;
            }
        }
        return (uint)(slot.Tll + EnvCurve[slot.Evc >> EnvBits] + (slot.Ams != 0 ? _ams : 0));
    }

    private static void CalcFcSlot(Channel channel, Slot slot)
    {
        slot.Incr = channel.Fc * slot.Mul;
        var ksr = (byte)(channel.Kcode >> slot.Ksr);
        if (slot.KsrValue != ksr)
        {
            slot.KsrValue = ksr;
            slot.Evsa = slot.Ar[slot.ArOff + ksr];
            slot.Evsd = slot.Dr[slot.DrOff + ksr];
            slot.Evsr = slot.Rr[slot.RrOff + ksr];
        }
        slot.Tll = slot.Tl + (int)(channel.KslBase >> slot.KslShift);
    }

    // ---- register decoding ----

    private void SetMul(int slotNumber, int v)
    {
        var channel = _channels[slotNumber >> 1];
        var slot = channel.Slots[slotNumber & 1];
        slot.Mul = MulTable[v & 0x0f];
        slot.Ksr = (byte)((v & 0x10) != 0 ? 0 : 2);
        slot.EgTyp = (byte)((v & 0x20) >> 5);
        slot.Vib = (byte)(v & 0x40);
        slot.Ams = (byte)(v & 0x80);
        CalcFcSlot(channel, slot);
    }

    private void SetKslTl(int slotNumber, int v)
    {
        var channel = _channels[slotNumber >> 1];
        var slot = channel.Slots[slotNumber & 1];
        var ksl = v >> 6; // ScummVM's reading: 0 / 1.5 / 3 / 6 dB per octave (the chip documents 1 = 3 and 2 = 1.5)
        slot.KslShift = (byte)(ksl != 0 ? 3 - ksl : 31);
        slot.Tl = (int)((v & 0x3f) * (0.75 / EgStep));
        slot.Tll = slot.Tl + (int)(channel.KslBase >> slot.KslShift);
    }

    private void SetArDr(int slotNumber, int v)
    {
        var slot = _channels[slotNumber >> 1].Slots[slotNumber & 1];
        var ar = v >> 4;
        var dr = v & 0x0f;
        if (ar != 0) { slot.Ar = _arTable; slot.ArOff = ar << 2; } else { slot.Ar = Rate0; slot.ArOff = 0; }
        slot.Evsa = slot.Ar[slot.ArOff + slot.KsrValue];
        if (slot.Evm == EnvModAr) slot.Evs = slot.Evsa;
        if (dr != 0) { slot.Dr = _drTable; slot.DrOff = dr << 2; } else { slot.Dr = Rate0; slot.DrOff = 0; }
        slot.Evsd = slot.Dr[slot.DrOff + slot.KsrValue];
        if (slot.Evm == EnvModDr) slot.Evs = slot.Evsd;
    }

    private void SetSlRr(int slotNumber, int v)
    {
        var slot = _channels[slotNumber >> 1].Slots[slotNumber & 1];
        var sl = v >> 4;
        var rr = v & 0x0f;
        slot.Sl = SlTable[sl];
        if (slot.Evm == EnvModDr) slot.Eve = slot.Sl;
        slot.Rr = _drTable;
        slot.RrOff = rr << 2;
        slot.Evsr = slot.Rr[slot.RrOff + slot.KsrValue];
        if (slot.Evm == EnvModRr) slot.Evs = slot.Evsr;
    }

    private void WriteReg(int r, int v)
    {
        v &= 0xff;
        r &= 0xff;
        switch (r & 0xe0)
        {
            case 0x00:
                if ((r & 0x1f) == 0x01)
                {
                    _waveSel = (byte)(v & 0x20);
                    if (_waveSel == 0)
                        foreach (var channel in _channels)
                            channel.Slots[0].WaveBase = channel.Slots[1].WaveBase = 0;
                }
                return; // timers and IRQ do not affect the sound
            case 0x20:
            {
                var slot = SlotArray[r & 0x1f];
                if (slot == -1) return;
                SetMul(slot, v);
                return;
            }
            case 0x40:
            {
                var slot = SlotArray[r & 0x1f];
                if (slot == -1) return;
                SetKslTl(slot, v);
                return;
            }
            case 0x60:
            {
                var slot = SlotArray[r & 0x1f];
                if (slot == -1) return;
                SetArDr(slot, v);
                return;
            }
            case 0x80:
            {
                var slot = SlotArray[r & 0x1f];
                if (slot == -1) return;
                SetSlRr(slot, v);
                return;
            }
            case 0xa0:
            {
                if (r == 0xbd)
                {
                    var rkey = (byte)(_rhythm ^ v);
                    _amsTableBase = (v & 0x80) != 0 ? AmsEnt : 0;
                    _vibTableBase = (v & 0x40) != 0 ? VibEnt : 0;
                    _rhythm = (byte)(v & 0x3f);
                    if ((_rhythm & 0x20) != 0)
                    {
                        if ((rkey & 0x10) != 0)
                        {
                            if ((v & 0x10) != 0)
                            {
                                _channels[6].Op1Out[0] = _channels[6].Op1Out[1] = 0;
                                KeyOn(_channels[6].Slots[0]);
                                KeyOn(_channels[6].Slots[1]);
                            }
                            else
                            {
                                KeyOff(_channels[6].Slots[0]);
                                KeyOff(_channels[6].Slots[1]);
                            }
                        }
                        if ((rkey & 0x08) != 0) { if ((v & 0x08) != 0) KeyOn(_channels[7].Slots[1]); else KeyOff(_channels[7].Slots[1]); }
                        if ((rkey & 0x04) != 0) { if ((v & 0x04) != 0) KeyOn(_channels[8].Slots[0]); else KeyOff(_channels[8].Slots[0]); }
                        if ((rkey & 0x02) != 0) { if ((v & 0x02) != 0) KeyOn(_channels[8].Slots[1]); else KeyOff(_channels[8].Slots[1]); }
                        if ((rkey & 0x01) != 0) { if ((v & 0x01) != 0) KeyOn(_channels[7].Slots[0]); else KeyOff(_channels[7].Slots[0]); }
                    }
                    return;
                }
                if ((r & 0x0f) > 8) return;
                var channel = _channels[r & 0x0f];
                uint blockFnum;
                if ((r & 0x10) == 0) blockFnum = (channel.BlockFnum & 0x1f00) | (uint)v;
                else
                {
                    var keyOn = (v >> 5) & 1;
                    blockFnum = (uint)(((v & 0x1f) << 8) | (int)(channel.BlockFnum & 0xff));
                    if (channel.KeyOn != keyOn)
                    {
                        channel.KeyOn = (byte)keyOn;
                        if (keyOn != 0)
                        {
                            channel.Op1Out[0] = channel.Op1Out[1] = 0;
                            KeyOn(channel.Slots[0]);
                            KeyOn(channel.Slots[1]);
                        }
                        else
                        {
                            KeyOff(channel.Slots[0]);
                            KeyOff(channel.Slots[1]);
                        }
                    }
                }
                if (channel.BlockFnum != blockFnum)
                {
                    var blockRv = 7 - (int)(blockFnum >> 10);
                    var fnum = (int)(blockFnum & 0x3ff);
                    channel.BlockFnum = blockFnum;
                    channel.KslBase = KslTable[blockFnum >> 6];
                    channel.Fc = _fnTable[fnum] >> blockRv;
                    channel.Kcode = (byte)(channel.BlockFnum >> 9);
                    CalcFcSlot(channel, channel.Slots[0]);
                    CalcFcSlot(channel, channel.Slots[1]);
                }
                return;
            }
            case 0xc0:
            {
                if ((r & 0x0f) > 8) return;
                var channel = _channels[r & 0x0f];
                var feedback = (v >> 1) & 7;
                channel.Fb = (byte)(feedback != 0 ? (8 + 1) - feedback : 0);
                channel.Con = (byte)(v & 1);
                return;
            }
            case 0xe0:
            {
                var slot = SlotArray[r & 0x1f];
                if (slot == -1) return;
                var channel = _channels[slot >> 1];
                if (_waveSel != 0) channel.Slots[slot & 1].WaveBase = (v & 0x03) * SinEnt;
                return;
            }
        }
    }

    // ---- sample generation ----

    private int OpOut(Slot slot, uint env, int con) =>
        TlTable[SinTable[slot.WaveBase + (int)(((slot.Cnt + (uint)con) >> (24 - SinEntShift)) & (SinEnt - 1))] + (int)env];

    private void CalcChannel(Channel channel)
    {
        _feedback2 = 0;
        var slot = channel.Slots[0];
        var envOut = CalcSlot(slot);
        if (envOut < (uint)(EgEnt - 1))
        {
            slot.Cnt += slot.Vib != 0 ? (uint)((slot.Incr * (uint)_vib) >> VibRateShift) : slot.Incr;
            int output;
            if (channel.Fb != 0)
            {
                var feedback1 = (channel.Op1Out[0] + channel.Op1Out[1]) >> channel.Fb;
                channel.Op1Out[1] = channel.Op1Out[0];
                output = channel.Op1Out[0] = OpOut(slot, envOut, feedback1);
            }
            else output = OpOut(slot, envOut, 0);
            if (channel.Con != 0) _outd += output; else _feedback2 += output;
        }
        else
        {
            channel.Op1Out[1] = channel.Op1Out[0];
            channel.Op1Out[0] = 0;
        }

        slot = channel.Slots[1];
        envOut = CalcSlot(slot);
        if (envOut < (uint)(EgEnt - 1))
        {
            slot.Cnt += slot.Vib != 0 ? (uint)((slot.Incr * (uint)_vib) >> VibRateShift) : slot.Incr;
            _outd += OpOut(slot, envOut, _feedback2);
        }
    }

    private int NextNoiseBit()
    {
        _noise = _noise * 1103515245u + 12345u;
        return (int)((_noise >> 16) & 1);
    }

    private void CalcRhythm()
    {
        var ch = _channels;
        var whiteNoise = NextNoiseBit() * (EgEnt >> 4);
        var slot7a = ch[7].Slots[0];
        var slot7b = ch[7].Slots[1];
        var slot8a = ch[8].Slots[0];
        var slot8b = ch[8].Slots[1];

        _feedback2 = 0;
        var slot = ch[6].Slots[0];
        var envOut = CalcSlot(slot);
        if (envOut < EgEnt - 1)
        {
            slot.Cnt += slot.Vib != 0 ? (uint)((slot.Incr * (uint)_vib) >> VibRateShift) : slot.Incr;
            if (ch[6].Fb != 0)
            {
                var feedback1 = (ch[6].Op1Out[0] + ch[6].Op1Out[1]) >> ch[6].Fb;
                ch[6].Op1Out[1] = ch[6].Op1Out[0];
                _feedback2 = ch[6].Op1Out[0] = OpOut(slot, envOut, feedback1);
            }
            else _feedback2 = OpOut(slot, envOut, 0);
        }
        else
        {
            _feedback2 = 0;
            ch[6].Op1Out[1] = ch[6].Op1Out[0];
            ch[6].Op1Out[0] = 0;
        }
        slot = ch[6].Slots[1];
        envOut = CalcSlot(slot);
        if (envOut < EgEnt - 1)
        {
            slot.Cnt += slot.Vib != 0 ? (uint)((slot.Incr * (uint)_vib) >> VibRateShift) : slot.Incr;
            _outd += OpOut(slot, envOut, _feedback2) * 2;
        }

        var envSd = CalcSlot(slot7b) + (uint)whiteNoise;
        var envTam = CalcSlot(slot8a);
        var envTop = CalcSlot(slot8b);
        var envHh = CalcSlot(slot7a) + (uint)whiteNoise;

        slot7a.Cnt += slot7a.Vib != 0 ? (uint)((slot7a.Incr * (uint)_vib) >> (VibRateShift - 1)) : 2 * slot7a.Incr;
        slot7b.Cnt += slot7b.Vib != 0 ? (uint)((ch[7].Fc * (uint)_vib) >> (VibRateShift - 3)) : ch[7].Fc * 8;
        slot8a.Cnt += slot8a.Vib != 0 ? (uint)((slot8a.Incr * (uint)_vib) >> VibRateShift) : slot8a.Incr;
        slot8b.Cnt += slot8b.Vib != 0 ? (uint)(((ch[8].Fc * 3) * (uint)_vib) >> (VibRateShift - 4)) : ch[8].Fc * 48;

        var tone8 = OpOut(slot8b, (uint)whiteNoise, 0);
        if (envSd < (uint)(EgEnt - 1)) _outd += OpOut(slot7a, envSd, 0) * 8;
        if (envTam < (uint)(EgEnt - 1)) _outd += OpOut(slot8a, envTam, 0) * 2;
        if (envTop < (uint)(EgEnt - 1)) _outd += OpOut(slot7b, envTop, tone8) * 2;
        if (envHh < (uint)(EgEnt - 1)) _outd += OpOut(slot7b, envHh, tone8) * 2;
    }

    private void UpdateOne(short[] buffer, int length)
    {
        if (!_lfoCaptured)
        {
            // ScummVM copies the LFO table pointers when the chip is first used and does not refresh them.
            _lfoCaptured = true;
            _amsBase = _amsTableBase;
            _vibBase = _vibTableBase;
        }
        var rhythm = (_rhythm & 0x20) != 0;
        var fmChannels = rhythm ? 6 : 9;
        for (var i = 0; i < length; i++)
        {
            _amsCnt += (uint)_amsIncr;
            _ams = AmsTable[_amsBase + (int)(_amsCnt >> AmsShift)];
            _vibCnt += (uint)_vibIncr;
            _vib = VibTable[_vibBase + (int)(_vibCnt >> VibShift)];
            _outd = 0;
            for (var c = 0; c < fmChannels; c++) CalcChannel(_channels[c]);
            if (rhythm) CalcRhythm();
            var data = Math.Clamp(_outd, MinOut, MaxOut);
            buffer[i] = (short)(data >> OutShift);
        }
    }
}

using System.Buffers.Binary;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;

// Mt32Probe <gamefolder>: the Roland ("RO") streams of sounds 33..64: events, channels, programs, length in ticks.
var game = new GameFolderScanner().Scan(args[0]);
if (args.Length > 2 && args[1] == "activity")
{
    // Mt32Probe <game> activity <sound>: notes started and channels used per 5 s window, and the markers in it.
    var res = game.Resources.First(r => r.Kind == ResourceKind.Sound && r.ResourceId == int.Parse(args[2]));
    var song = SCUMMexplorer.Core.Formats.LoomRolandSong.Read(res);
    var spt = SCUMMexplorer.Core.Formats.LoomRolandSong.SecondsPerTick;
    for (int w = 0; w * 5 < song.Duration.TotalSeconds + 5; w++)
    {
        var ev = song.Events.Where(e => e.Tick * spt >= w * 5 && e.Tick * spt < w * 5 + 5).ToList();
        var notes = ev.Count(e => e.Command == 9 && e.Data2 > 0);
        var mk = song.MarkerTicks.Count(t => t * spt >= w * 5 && t * spt < w * 5 + 5);
        var progs = string.Join(",", ev.Where(e => e.Command == 0xC).Select(e => $"{e.Channel}:{e.Data1}"));
        Console.WriteLine($"{w * 5,4}s notes {notes,3} ch {string.Join("", ev.Where(e => e.Command == 9).Select(e => e.Channel).Distinct().OrderBy(c => c))} markers {mk} {progs}");
    }
    return;
}
if (args.Length > 2 && args[1] == "markers")
{
    // Mt32Probe <game> markers <sound>: where the song's markers fall (the game's music timer counts them).
    var res = game.Resources.First(r => r.Kind == ResourceKind.Sound && r.ResourceId == int.Parse(args[2]));
    var song = SCUMMexplorer.Core.Formats.LoomRolandSong.Read(res);
    Console.WriteLine($"{song.MarkerTicks.Count} markers, song {song.Duration.TotalSeconds:F1}s ({song.Ticks} ticks)");
    foreach (var n in new[] { 1, 4, 5, 50, 100, 150, 198, 199, 204, 205, 250, song.MarkerTicks.Count })
        if (n >= 1 && n <= song.MarkerTicks.Count) Console.WriteLine($"marker {n}: tick {song.MarkerTicks[n - 1]} = {song.MarkerTicks[n - 1] * SCUMMexplorer.Core.Formats.LoomRolandSong.SecondsPerTick:F1}s");
    return;
}
if (args.Length > 1 && args[1] == "timing")
{
    // Mt32Probe <game> timing: how long a fresh session and the first visit of Room 1 take.
    var snap = new SCUMMexplorer.Core.Formats.LflObjectStateReader().ReadSnapshot(Path.Combine(game.FolderPath, "00.LFL"));
    var sw = System.Diagnostics.Stopwatch.StartNew();
    var card = args.Length > 2 ? int.Parse(args[2]) : 0;
    var session = new SCUMMexplorer.Core.Formats.LoomExplorationSession(game, snap, soundCard: card);
    Console.WriteLine($"new session (boot completed): {sw.ElapsedMilliseconds} ms, room {session.Snapshot.RoomId}");
    sw.Restart();
    var room = game.Rooms.First(r => r.ResourceId == 1);
    session.VisitRoom(room);
    Console.WriteLine($"VisitRoom(1): {sw.ElapsedMilliseconds} ms");
    sw.Restart();
    var s2 = new SCUMMexplorer.Core.Formats.LoomExplorationSession(game, snap, stopAtOpening: true);
    Console.WriteLine($"new session stopAtOpening: {sw.ElapsedMilliseconds} ms");
    return;
}
if (args.Length > 2 && args[1] == "opening")
{
    // Mt32Probe <game> opening <card> [frames]: the opening under a sound card, state every 100 frames, until it has run its course.
    var card = int.Parse(args[2]); int limit = args.Length > 3 ? int.Parse(args[3]) : 6000;
    var snap = new SCUMMexplorer.Core.Formats.LflObjectStateReader().ReadSnapshot(Path.Combine(game.FolderPath, "00.LFL"));
    var session = new SCUMMexplorer.Core.Formats.LoomExplorationSession(game, snap, stopAtOpening: true, soundCard: card);
    int lastRoom = -1, shown = 0;
    for (int frame = 1; frame <= limit; frame++)
    {
        session.AdvanceFrame();
        var sn = session.Snapshot;
        while (shown < session.SoundEvents.Count)
        {
            var ev = session.SoundEvents[shown++].Event;
            Console.WriteLine($"frame {frame}: SOUND {ev.Kind} {ev.TargetId} <- {ev.SourceLabel} +0x{ev.Offset:X4} {ev.Operation} {ev.Operands}");
        }
        if (sn.RoomId != lastRoom || frame % 200 == 0)
        {
            lastRoom = sn.RoomId ?? -1;
            Console.WriteLine($"frame {frame}: room {sn.RoomId}, boot {session.BootRunning}, cutscene {session.InCutscene}, pending {sn.PendingScripts}, sound a3 {sn.KnownGlobalVariables.GetValueOrDefault("var[0x0A3]")}, jiffies {session.FrameJiffies}");
        }
        if (!session.BootRunning && !session.InCutscene && sn.PendingScripts == 0) { Console.WriteLine($"frame {frame}: quiet (boot over, no cutscene, nothing pending) in room {sn.RoomId}"); break; }
    }
    return;
}
if (args.Length > 1 && args[1] == "compare")
{
    // Roland length against the AdLib sound 32 lower (script 039/040 add 0x20 to the id on a Roland card).
    var ad = new SCUMMexplorer.Core.Formats.LoomAdlibAudio();
    foreach (var r in game.Resources.Where(r => r.Kind == ResourceKind.Sound && r.ResourceId is >= 33).OrderBy(r => r.ResourceId))
    {
        var song = SCUMMexplorer.Core.Formats.LoomRolandSong.Read(r);
        var a = game.Resources.FirstOrDefault(x => x.Kind == ResourceKind.Sound && x.ResourceId == r.ResourceId - 32);
        string adl = "-";
        if (a is not null) { try { adl = ad.HasMusic(a) ? ad.EstimateDuration(a).TotalSeconds.ToString("F1") + "s music" : "effect/other"; } catch (Exception ex) { adl = ex.GetType().Name; } }
        Console.WriteLine($"roland {r.ResourceId}: {song.Duration.TotalSeconds:F1}s loops={song.Loops} end={song.HasEndMarker} | adlib {r.ResourceId - 32}: {adl}");
    }
    return;
}
if (args.Length > 3 && args[1] == "render")
{
    // Mt32Probe <game> render <romfolder> <outfolder> <ids...>: WAV per Roland sound through the MT-32 emulator.
    var roms = SCUMMexplorer.Core.Formats.Mt32RomSet.Find(args[2]) ?? throw new FileNotFoundException("no ROM set in " + args[2]);
    Console.WriteLine($"ROMs: {roms.Model} {roms.ControlPath} + {roms.PcmPath}");
    Directory.CreateDirectory(args[3]);
    var audio = new SCUMMexplorer.Core.Formats.LoomMt32Audio(roms);
    foreach (var id in args.Skip(4).Select(int.Parse))
    {
        var res = game.Resources.First(r => r.Kind == ResourceKind.Sound && r.ResourceId == id);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var wave = audio.RenderWave(res);
        var rate = BitConverter.ToInt32(wave, 24); int peak = 0; long nonzero = 0;
        for (int i = 44; i + 1 < wave.Length; i += 2) { int v = Math.Abs(BitConverter.ToInt16(wave, i)); if (v > peak) peak = v; if (v > 200) nonzero++; }
        File.WriteAllBytes(Path.Combine(args[3], $"sound-{id}.wav"), wave);
        Console.WriteLine($"sound {id}: {rate} Hz, {(wave.Length - 44) / 4.0 / rate:F1}s, peak {peak}, loud samples {nonzero}, rendered in {sw.ElapsedMilliseconds} ms");
    }
    return;
}
var cache = new Dictionary<string, byte[]>();
byte[] Load(string p) { if (!cache.TryGetValue(p, out var b)) { b = File.ReadAllBytes(p); for (int i = 0; i < b.Length; i++) b[i] ^= 0xFF; cache[p] = b; } return b; }
foreach (var r in game.Resources.Where(r => r.Kind == ResourceKind.Sound && r.ResourceId is >= 33 && r.Offset is not null).OrderBy(r => r.ResourceId))
{
    var f = Load(r.FilePath); int off = r.Offset!.Value; int len = BinaryPrimitives.ReadUInt16LittleEndian(f.AsSpan(off, 2));
    var d = f.AsSpan(off + 4, len - 4); int p = 0; long ticks = 0; int markers = 0, notes = 0; var chans = new SortedSet<int>(); var progs = new SortedDictionary<int, int>(); string end = "end-of-data"; var other = new SortedSet<string>();
    while (p < d.Length)
    {
        int e = d[p++];
        if (e == 0) { end = "EOT"; break; }
        if (e == 0xFF) { end = "LOOP"; break; }
        if (e >> 4 == 0xA) { markers++; continue; }
        if (e == 0xF0 || e == 0xF1) { ticks += d[p++] + (e == 0xF1 ? 256 : 0); continue; }
        if (e < 0x80) { other.Add($"data {e:X2}"); continue; }
        int c = e & 15; chans.Add(c);
        switch (e >> 4)
        {
            case 0xC: progs[c * 1000 + d[p++]] = 1; break;
            case 0x8: case 0x9: p += 2; if (e >> 4 == 9) notes++; break;
            case 0xB: if (d[p] != 7 && d[p] != 10) other.Add($"CC{d[p]}"); p += 2; break;
            default: other.Add($"evt {e:X2}"); p++; break;
        }
    }
    Console.WriteLine($"sound {r.ResourceId}: {len} bytes, {ticks} ticks ({ticks * 500000.0 / 120 / 1e6:F1}s), markers {markers}, notes {notes}, {end}, ch [{string.Join(',', chans)}], progs [{string.Join(' ', progs.Keys.Select(k => $"{k / 1000}:{k % 1000}"))}] {string.Join(' ', other)}");
}

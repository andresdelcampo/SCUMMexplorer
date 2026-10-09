using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;

var game = new GameFolderScanner().Scan(args[0]);
var sounds = game.Resources.Where(r => r.Kind == ResourceKind.Sound && r.ResourceId.HasValue)
    .GroupBy(r => r.ResourceId!.Value).ToDictionary(g => g.Key, g => g.First());
var audio = new ScummAdlibAudio();
var candidates = new LoomRoomSoundCandidates();
var records = new ScummRoomObjectDirectory();
var summary = new List<(int Room, string Tracks)>();
foreach (var room in game.Rooms.OrderBy(r => r.ResourceId))
{
    IReadOnlyList<ScummRoomObjectRecord> roomRecords;
    try { roomRecords = records.Decode(File.ReadAllBytes(room.FilePath)); }
    catch (Exception ex) when (ex is IOException or InvalidDataException) { roomRecords = []; }
    var tracks = new Dictionary<int, double>();
    foreach (var id in candidates.Analyze(game, room, roomRecords).Where(c => c.SoundId.HasValue)
                 .Select(c => c.SoundId!.Value).Distinct())
    {
        if (!sounds.TryGetValue(id, out var sound) || !audio.HasMusic(sound)) continue;
        try { tracks[id] = audio.EstimateDuration(sound).TotalSeconds; }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException) { }
    }
    summary.Add((room.ResourceId ?? -1, string.Join(", ", tracks.OrderByDescending(p => p.Value)
        .ThenBy(p => p.Key).Select(p => $"{p.Key}:{p.Value:F1}s"))));
}
foreach (var cutoff in new[] { 5, 15 })
{
    var counts = summary.Select(room => room.Tracks.Split(',', StringSplitOptions.RemoveEmptyEntries)
        .Select(track => double.Parse(track[(track.IndexOf(':') + 1)..].TrimEnd('s'), System.Globalization.CultureInfo.InvariantCulture))
        .Count(seconds => seconds >= cutoff)).ToArray();
    Console.WriteLine($">={cutoff}s: zero={counts.Count(n => n == 0)}, one={counts.Count(n => n == 1)}, multiple={counts.Count(n => n > 1)}");
    foreach (var room in summary.Zip(counts).Where(pair => pair.Second > 1))
        Console.WriteLine($"  Room {room.First.Room:00}: {room.First.Tracks}");
    if (cutoff == 15)
        Console.WriteLine("  No qualifying track: " + string.Join(", ", summary.Zip(counts).Where(pair => pair.Second == 0).Select(pair => pair.First.Room.ToString("00"))));
}

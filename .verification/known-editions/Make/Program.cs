// Makes src/SCUMMexplorer.Core/Discovery/known-editions.json from the local game copies, in every format.
// Usage: dotnet run --project .verification/known-editions/Make   (from the repository root; SCUMM_GAMES_ROOT or D:\Juegos\Lucas holds the copies)
// Per edition: up to 14 data files (taken so that no two editions of a game share the whole sample) with their SHA-256, as GameFingerprint hashes them
// (the same code path the app identifies with, so a disc image or a language folder is fingerprinted exactly as the app will). Names match case-insensitively.
using System.Text.Json;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;

var root = Environment.GetEnvironmentVariable("SCUMM_GAMES_ROOT") is { Length: > 0 } r ? r : @"D:\Juegos\Lucas";
(string Folder, KnownGame Game, string Label, bool Demo)[] editions =
[
    ("loom ega mt32 en", KnownGame.Loom, "English, Roland upgrade", false),
    ("loom ega en", KnownGame.Loom, "English", false),
    ("loom ega es", KnownGame.Loom, "Spanish", false),
    ("loom ega mt32 es", KnownGame.Loom, "Spanish, Roland upgrade", false),
    ("loom ega fr", KnownGame.Loom, "French", false),
    ("loom ega de", KnownGame.Loom, "German", false),
    ("loom ega v1.2 es", KnownGame.Loom, "Spanish, version 1.2", false),
    ("loom ega it", KnownGame.Loom, "Italian", false),
    ("loom demo long ega en", KnownGame.Loom, "English, long demo", true),
    ("loom demo short ega en", KnownGame.Loom, "English, short demo", true),
    ("loom vga en", KnownGame.Loom, "English, CD", false),
    // "loom vga en kixx" holds the same data as "loom vga en"; "mi2 mt32 en" the same as "mi2 sb16 en" (they differ in sound ROMs, not game data): one edition each.
    ("indy3 ega en", KnownGame.IndianaJones3, "English", false),
    ("indy3 ega es", KnownGame.IndianaJones3, "Spanish", false),
    ("indy3 demo en", KnownGame.IndianaJones3, "English, demo", true),
    ("indy3 vga en", KnownGame.IndianaJones3, "English, 256 colours", false),
    ("indy3 vga es", KnownGame.IndianaJones3, "Spanish, 256 colours", false),
    ("indy4 vga en", KnownGame.IndianaJones4, "English, CD", false),
    ("indy4 classic vga es", KnownGame.IndianaJones4, "Spanish", false),
    ("indy4 talkie vga es", KnownGame.IndianaJones4, "Spanish, talkie", false),
    ("monkey1 ega en", KnownGame.MonkeyIsland, "English, EGA", false),
    ("monkey1 ega es", KnownGame.MonkeyIsland, "Spanish, EGA", false),
    ("monkey1 vga es", KnownGame.MonkeyIsland, "Spanish, VGA", false),
    ("monkey1 vga en", KnownGame.MonkeyIsland, "English, CD", false),
    ("mi2 classic es", KnownGame.MonkeyIsland2, "Spanish", false),
    ("mi2 sb16 en", KnownGame.MonkeyIsland2, "English", false),
    ("mi2 talkie en", KnownGame.MonkeyIsland2, "English, talkie", false),
    ("mi2 talkie es", KnownGame.MonkeyIsland2, "Spanish, talkie", false),
    ("mmansion ega en", KnownGame.ManiacMansion, "English, Maniac Mansion classic", false),
    ("mmansionE ega es", KnownGame.ManiacMansion, "Spanish, Maniac Mansion enhanced", false),
    ("zak ega en", KnownGame.ZakMcKracken, "English, Zak McKracken classic", false),
    ("zakE ega en", KnownGame.ZakMcKracken, "English, Zak McKracken enhanced", false),
];

var data = new List<(string Folder, KnownGame Game, string Label, bool Demo, SortedDictionary<string, string> Hashes, DirectoryShape? Shape)>();
foreach (var (folder, game, label, demo) in editions)
{
    var path = Path.Combine(root, folder);
    if (!Directory.Exists(path)) { Console.WriteLine("missing, skipped: " + folder); continue; }
    // A disc with one copy of the game per language folder is one edition per language (the label's language is replaced by the folder's name).
    var languages = GameFolderScanner.ListLanguages(path);
    foreach (var (key, languageLabel) in languages.Count == 0 ? [((string?)null, label)] : languages.Select(l => ((string?)l.Key, $"{l.Label}{label[(label.IndexOf(',') < 0 ? label.Length : label.IndexOf(','))..]}")))
    {
        var fingerprint = GameFolderScanner.Fingerprint(path, key)!;
        data.Add((key is null ? folder : folder + "|" + key, game, languageLabel, demo, new(fingerprint.FileNames.ToDictionary(n => n.ToUpperInvariant(), n => fingerprint.Hash(n) ?? ""), StringComparer.Ordinal), fingerprint.Shape));
    }
}

var output = new List<object>();
foreach (var group in data.GroupBy(e => e.Game))
    foreach (var edition in group)
    {
        var others = group.Where(o => o.Folder != edition.Folder).ToList();
        var h = edition.Hashes;
        // Files that tell this edition from each other one, most telling first.
        int Telling(string name) => others.Count(o => !o.Hashes.TryGetValue(name, out var x) || x != h[name]);
        var names = h.Keys.OrderBy(n => -Telling(n)).ThenBy(n => n, StringComparer.Ordinal).ToList();
        var sample = new List<string>();
        // First make sure every other edition differs from this one in at least two sample files where the data allows, then fill with the most telling.
        foreach (var need in new[] { 1, 2 })
            foreach (var o in others)
                while (sample.Count(n => !o.Hashes.TryGetValue(n, out var x) || x != h[n]) < need)
                {
                    var candidate = names.FirstOrDefault(n => !sample.Contains(n) && (!o.Hashes.TryGetValue(n, out var x) || x != h[n]));
                    if (candidate is null) break;
                    sample.Add(candidate);
                }
        foreach (var n in names)
            if (sample.Count < 14 && Telling(n) > 0 && !sample.Contains(n)) sample.Add(n);
        if (sample.Count == 0) sample.AddRange(names.Take(14)); // an edition no other of its game differs from in any file
        foreach (var o in others)
            if (!sample.Any(n => !o.Hashes.TryGetValue(n, out var x) || x != h[n]))
                Console.WriteLine($"not told apart by this edition's own files: {edition.Label} ({edition.Game}) <- {o.Label}");
        output.Add(new { game = edition.Game.ToString(), label = edition.Label, demo = edition.Demo, dataFiles = h.Count, files = sample.Order(StringComparer.Ordinal).ToDictionary(n => n, n => h[n]) });
    }
// Per game, what its editions share, so an edition the catalog lacks (another language) is still told: (1) per format, the data files present in every
// non-demo edition in at most two versions; (2) per format, the directory sizes every non-demo edition has. Both must match no other game's editions.
var games = new List<object>();
foreach (var group in data.GroupBy(e => e.Game))
{
    var others = data.Where(e => e.Game != group.Key).ToList();
    var shared = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
    var shapes = new List<object>();
    foreach (var format in group.Where(e => !e.Demo && e.Shape is not null).GroupBy(e => e.Shape!.Version))
    {
        var editionsOfFormat = format.ToList();
        if (editionsOfFormat.Count >= 2)
        {
            var candidates = editionsOfFormat[0].Hashes.Keys.Where(n => editionsOfFormat.All(e => e.Hashes.ContainsKey(n)))
                .Select(n => (Name: n, Hashes: editionsOfFormat.Select(e => e.Hashes[n]).Distinct().ToList()))
                .Where(c => c.Hashes.Count <= 2 && !others.Any(o => o.Hashes.TryGetValue(c.Name, out var x) && c.Hashes.Contains(x)))
                .OrderBy(c => c.Hashes.Count).ThenBy(c => c.Name, StringComparer.Ordinal).Take(6);
            foreach (var c in candidates) shared[c.Name] = new SortedSet<string>(c.Hashes, StringComparer.Ordinal);
        }
        int? Common(Func<DirectoryShape, int> pick) => editionsOfFormat.Select(e => pick(e.Shape!)).Distinct().Count() == 1 ? pick(editionsOfFormat[0].Shape!) : null;
        var known = new KnownShape(format.Key, Common(x => x.Rooms), Common(x => x.Costumes), Common(x => x.Scripts), Common(x => x.Sounds));
        if (known.Rooms is null && known.Costumes is null && known.Scripts is null && known.Sounds is null) continue;
        var clash = others.FirstOrDefault(o => o.Shape is not null && known.Matches(o.Shape));
        if (clash.Label is not null) { Console.WriteLine($"shape of {group.Key} {format.Key} also matches {clash.Game} {clash.Label}: not used"); continue; }
        shapes.Add(new { version = format.Key.ToString(), rooms = known.Rooms, costumes = known.Costumes, scripts = known.Scripts, sounds = known.Sounds });
    }
    games.Add(new { game = group.Key.ToString(), sharedFiles = shared.ToDictionary(p => p.Key, p => p.Value.ToArray()), shapes });
}
var target = Path.Combine("src", "SCUMMexplorer.Core", "Discovery", "known-editions.json");
File.WriteAllText(target, JsonSerializer.Serialize(new { editions = output, games }, new JsonSerializerOptions { WriteIndented = true, IndentSize = 1 }));
Console.WriteLine($"{output.Count} editions");

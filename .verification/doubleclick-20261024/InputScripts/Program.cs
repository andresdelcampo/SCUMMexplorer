using System.Text.RegularExpressions;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Scumm;

// Does a game's input chain use a double click? Starting from the input script (VAR_VERB_SCRIPT in play) it follows the
// startScript/chainScript calls with a literal script id (to a depth) and dumps every script reached, then lists where the
// timers (var[0x00B]..var[0x00D], VAR_TMR_1..3, which LOOM's script 006 uses for its double-click window) are read or set.
// Usage: InputScripts <game folder> <output file> <depth> <first script id> [more ids]
var game = new GameFolderScanner().Scan(args[0]);
var data = ScummDataSources.For(game);
var depth = int.Parse(args[2]);
var output = new List<string> { $"# {game.Id} {args[0]}" };
var seen = new HashSet<int>();
var timers = new List<string>();
var queue = new Queue<(int Id, int Level)>(args.Skip(3).Select(id => (int.Parse(id), 0)));
var call = new Regex(@"(?:startScript|chainScript) 0x([0-9A-F]+)", RegexOptions.IgnoreCase);
var timer = new Regex(@"var\[0x00[BCD]\]|var\[0x00[BCD]\+", RegexOptions.IgnoreCase);
while (queue.Count > 0)
{
    var (id, level) = queue.Dequeue();
    if (!seen.Add(id)) continue;
    ScummScriptDisassembly? script;
    try { script = data.GlobalScript(id); } catch (Exception ex) { output.Add($"== script {id}: {ex.Message}"); continue; }
    if (script is null) continue;
    output.Add($"== script {id} (depth {level})");
    foreach (var i in script.Instructions)
    {
        var line = $"{i.Offset:X4} {i.Operation} {i.Operands}";
        output.Add(line);
        if (timer.IsMatch(line)) timers.Add($"script {id}: {line}");
        if (level < depth && call.Match(line) is { Success: true } match) queue.Enqueue((Convert.ToInt32(match.Groups[1].Value, 16), level + 1));
    }
}
output.Insert(1, $"scripts reached: {string.Join(", ", seen.Order())}");
output.Insert(2, $"timer references (var[0x00B..0x00D]): {timers.Count}");
foreach (var line in timers) output.Insert(3, "  " + line);
File.WriteAllLines(args[1], output);
Console.WriteLine($"{seen.Count} scripts, {timers.Count} timer references");

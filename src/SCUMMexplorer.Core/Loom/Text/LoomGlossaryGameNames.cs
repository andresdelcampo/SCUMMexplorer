using System.Globalization;
using System.Text.RegularExpressions;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Loom;

public sealed partial class LoomGlossary
{
    /// <summary>
    /// The names a game without reviewed names stores in its own data (every game but LOOM; ROADMAP item 3, 31 October 2026): each object's name from its
    /// room, and each actor's name from the <c>actorOps … actorName "…"</c> that gives it, scoped to the script that sets it (actor numbers are reused from
    /// scene to scene), plus a game-wide name for an actor when one name is given in at least two thirds of the scripts that name that actor. Read once,
    /// the first time a name is asked for (opening the game reads nothing more).
    /// </summary>
    private void ReadGameNames(GameLibrary game)
    {
        var data = ScummDataSources.For(game);
        foreach (var room in game.Rooms.Where(r => r.ResourceId is not null).Select(r => r.ResourceId!.Value).Distinct())
        {
            try
            {
                foreach (var (id, bytes) in data.ReadRoom(room).ObjectNames)
                {
                    var name = ScummTextDecoder.DecodeGlyphs(bytes).Trim().TrimEnd('@').Trim();
                    if (name.Length > 0) _objects.TryAdd(id, (room, name));
                }
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or NotSupportedException or ArgumentException) { }
        }
        var named = new List<(int Actor, string Name, string Scope, string Where)>();
        var verbs = new List<(int Actor, string Name, string Scope, string Where)>();
        var dressed = new List<(int Actor, int Costume, string Scope, string Where)>();
        void Scan(Func<ScummScriptDisassembly?> read, string scope, string label)
        {
            ScummScriptDisassembly? script;
            try { script = read(); }
            catch (Exception ex) when (ex is InvalidDataException or IOException or NotSupportedException or ArgumentException) { return; }
            if (script is null) return;
            foreach (var ins in script.Instructions.Where(i => i.Operation is "actorOps" or "verbOps"))
            {
                var first = ins.Operands.Split(',', 2)[0].Trim();
                if (!first.StartsWith("0x", StringComparison.Ordinal) ||
                    !int.TryParse(first.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var actor)) continue;
                if (ins.Operation == "actorOps" && CostumeRegex().Match(ins.Operands) is { Success: true } costume)
                    dressed.Add((actor, int.Parse(costume.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture), scope, $"{label} +0x{ins.Offset:X4}"));
                if ((ins.Operation == "actorOps" ? ActorNameRegex() : VerbNameRegex()).Match(ins.Operands) is not { Success: true } m) continue;
                var name = m.Groups[1].Value.Trim();
                // A name with control codes or no letter (a placeholder) is not a name.
                if (name.Length == 0 || name.Contains('\\') || !name.Any(char.IsLetter)) continue;
                (ins.Operation == "actorOps" ? named : verbs).Add((actor, name, scope, $"{label} +0x{ins.Offset:X4}"));
            }
        }
        foreach (var id in data.GlobalScriptIds()) Scan(() => data.GlobalScript(id), $"script:{id}", $"global script {id}");
        foreach (var room in game.Rooms.Where(r => r.ResourceId is not null).Select(r => r.ResourceId!.Value).Distinct())
        {
            Scan(() => data.RoomEntryScript(room), $"room:{room}", $"Room {room} entry script");
            Scan(() => data.RoomExitScript(room), $"room:{room}", $"Room {room} exit script");
            IReadOnlyList<int> locals;
            try { locals = data.RoomLocalScriptIds(room); }
            catch (Exception ex) when (ex is InvalidDataException or IOException or NotSupportedException or ArgumentException) { continue; }
            foreach (var local in locals) Scan(() => data.RoomLocalScript(room, local), $"room:{room}", $"Room {room} local script {local}");
        }
        if (!_byKind.TryGetValue("actor", out var rows)) _byKind["actor"] = rows = [];
        foreach (var scoped in named.GroupBy(n => (n.Actor, n.Scope)))
        {
            // A script that names the actor twice with two names (a disguise) keeps the first.
            var first = scoped.First();
            rows.Add((first.Actor, first.Actor, new(first.Name, GlossaryEvidence.Decoded, $"actorOps actorName at {first.Where}"), first.Scope));
        }
        // A costume belongs to one character more surely than an actor number does (Monkey Island 1 reuses actors 3 to 10 from scene to scene):
        // the name given to the actor wearing a costume, in a script or room that gives that actor one costume, names the costume when it is given
        // in at least two thirds of those places; a script or room that dresses an actor in it without naming the actor then uses it (1 November 2026).
        var costumeOf = dressed.GroupBy(d => (d.Actor, d.Scope)).Where(g => g.Select(d => d.Costume).Distinct().Count() == 1)
            .ToDictionary(g => g.Key, g => g.First());
        var costumeNames = named.Where(n => costumeOf.ContainsKey((n.Actor, n.Scope)))
            .Select(n => (Costume: costumeOf[(n.Actor, n.Scope)].Costume, n.Name, n.Scope, n.Where)).GroupBy(n => n.Costume)
            .Select(g => (Costume: g.Key, Scopes: g.Select(n => n.Scope).Distinct().Count(),
                Top: g.GroupBy(n => n.Name).Select(t => (Name: t.Key, Scopes: t.Select(n => n.Scope).Distinct().Count(), First: t.First().Where))
                    .OrderByDescending(t => t.Scopes).First()))
            .Where(c => c.Top.Scopes * 3 >= c.Scopes * 2).ToDictionary(c => c.Costume, c => c);
        var namedScopes = named.Select(n => (n.Actor, n.Scope)).ToHashSet();
        foreach (var ((actor, scope), dress) in costumeOf)
            if (!namedScopes.Contains((actor, scope)) && costumeNames.TryGetValue(dress.Costume, out var worn))
                rows.Add((actor, actor, new(worn.Top.Name, GlossaryEvidence.Decoded,
                    $"costume {dress.Costume} at {dress.Where}, worn by the actor named \"{worn.Top.Name}\" in {worn.Top.Scopes} of the {worn.Scopes} scripts that name its wearer (first at {worn.Top.First})"), scope));
        foreach (var verb in verbs.GroupBy(n => n.Actor))
        {
            var scopes = verb.Select(n => n.Scope).Distinct().Count();
            var top = verb.GroupBy(n => n.Name).Select(g => (Name: g.Key, Scopes: g.Select(n => n.Scope).Distinct().Count())).OrderByDescending(g => g.Scopes).First();
            if (top.Scopes * 3 >= scopes * 2) _verbNames[verb.Key] = top.Name;
        }
        foreach (var actor in named.GroupBy(n => n.Actor))
        {
            var scopes = actor.Select(n => n.Scope).Distinct().Count();
            var top = actor.GroupBy(n => n.Name).Select(g => (Name: g.Key, Scopes: g.Select(n => n.Scope).Distinct().Count(), First: g.First().Where))
                .OrderByDescending(g => g.Scopes).First();
            if (top.Scopes * 3 >= scopes * 2)
                rows.Add((actor.Key, actor.Key, new(top.Name, GlossaryEvidence.Decoded,
                    $"actorOps actorName in {top.Scopes} of the {scopes} scripts that name actor {actor.Key} (first at {top.First}); numbers are reused, so a script that names it otherwise uses its own"), null));
        }
    }

    /// <summary>
    /// A verb's name in this game (<c>verbOps N, [… name "…"]</c>): the name given in at least two thirds of the scripts that name the verb (dialogue
    /// choices reuse their numbers for every line, so they stay unnamed); null otherwise.
    /// </summary>
    public string? VerbName(int verb)
    {
        EnsureGameNames();
        return _verbNames.TryGetValue(verb, out var name) ? name : null;
    }

    private readonly Dictionary<int, string> _verbNames = [];

    [GeneratedRegex(@"(?:^|; |\[)name, ""([^""]*)""")]
    private static partial Regex VerbNameRegex();

    [GeneratedRegex(@"(?:\[|; )costume, 0x([0-9A-F]+)(?:;|\])")]
    private static partial Regex CostumeRegex();

    [GeneratedRegex("actorName, \"([^\"]*)\"")]
    private static partial Regex ActorNameRegex();
}

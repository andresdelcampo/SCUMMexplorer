using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Loom;

public sealed partial class LoomGlossary
{
    private sealed record ReviewedScript(int Id, string Name, string Evidence, string Source, string Fingerprint,
        List<ReviewedName>? Names, string? Edition = null);
    private sealed record ReviewedName(string Kind, int Id, string Name, string Evidence, string Source, string? Scope = null, int? To = null);

    // These addresses were read in this edition's instructions. They must bypass the older Spanish-layout shift.
    private readonly Dictionary<int, GlossaryEntry> _reviewedBits = new();

    // Each edition must match its reviewed instructions before receiving labels; a matching script ID is not enough.
    private void ReadCdReviewedNames(IScummDataSource data) => ReadReviewedNames(data, "loom-cd-glossary.json", false);

    private void ReadEgaReviewedNames(GameLibrary game)
    {
        var chain = new LflResourceChain();
        var interior = game.Resources.Where(r => r.Kind == ResourceKind.Script && r.ResourceId.HasValue &&
                                                 chain.FindLeftover(game, r) is not null)
            .Select(r => r.ResourceId!.Value).ToHashSet();
        ReadReviewedNames(ScummDataSources.For(game), "loom-ega-reviewed-glossary.json", true, interior);
    }

    private void ReadReviewedNames(IScummDataSource data, string resource, bool ega, IReadOnlySet<int>? interior = null)
    {
        using var stream = typeof(LoomGlossary).Assembly.GetManifestResourceStream("SCUMMexplorer.Core." + resource)
            ?? throw new InvalidOperationException("The script glossary review is missing from the assembly.");
        var reviewed = JsonSerializer.Deserialize<List<ReviewedScript>>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var scripts = new Dictionary<int, ScummScriptDisassembly?>();
        var applied = new HashSet<int>();
        foreach (var row in reviewed.OrderBy(row => ega && row.Edition == (_bitShift == 0 ? "Spanish EGA" : "English EGA") ? 0 : 1))
        {
            // Bytes inside another resource can accidentally decode completely and
            // match a review fingerprint. They do not establish a standalone script.
            if (applied.Contains(row.Id) || interior?.Contains(row.Id) == true) continue;
            if (!scripts.TryGetValue(row.Id, out var script))
            {
                try { script = data.GlobalScript(row.Id); }
                catch (InvalidDataException) { script = null; } // Some EGA index entries point outside the resource.
                scripts[row.Id] = script;
            }
            if (script is null || (ega && !script.HasCompleteControlFlow) || ReviewedFingerprint(script) != row.Fingerprint) continue;
            applied.Add(row.Id);
            Add("script", row.Id, row.Name, row.Evidence, row.Source, null, null);
            foreach (var name in row.Names ?? []) Add(name.Kind, name.Id, name.Name, name.Evidence, name.Source, name.Scope, name.To);
        }
        void Add(string kind, int id, string name, string evidence, string source, string? scope, int? to)
        {
            var entry = new GlossaryEntry(name, Enum.Parse<GlossaryEvidence>(evidence, true), source);
            if (ega && kind == "bit") { _reviewedBits[id] = entry; return; }
            if (!_byKind.TryGetValue(kind, out var rows)) _byKind[kind] = rows = [];
            if (ega) rows.Insert(0, (id, to ?? id, entry, scope));
            else rows.Add((id, to ?? id, entry, scope));
        }
    }

    internal static string ReviewedFingerprint(ScummScriptDisassembly script) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(string.Join("\n", script.Instructions.Select(i => i.Operation + " " + i.Operands)))));
}

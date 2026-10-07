using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SCUMMexplorer.Core.Formats;

public sealed partial class LoomGlossary
{
    private sealed record CdReviewedScript(int Id, string Name, string Evidence, string Source, string Fingerprint,
        List<CdReviewedName>? Names);
    private sealed record CdReviewedName(string Kind, int Id, string Name, string Evidence, string Source, string? Scope = null);

    // This is a review of the supplied CD instructions, with EGA as the starting reference. Another build must
    // match the reviewed instructions before receiving its labels; matching an EGA script number is never enough.
    private void ReadCdReviewedNames(IScummDataSource data)
    {
        using var stream = typeof(LoomGlossary).Assembly.GetManifestResourceStream("SCUMMexplorer.Core.loom-cd-glossary.json")
            ?? throw new InvalidOperationException("The CD glossary review is missing from the assembly.");
        var reviewed = JsonSerializer.Deserialize<List<CdReviewedScript>>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        foreach (var row in reviewed)
        {
            if (data.GlobalScript(row.Id) is not { } script || CdFingerprint(script) != row.Fingerprint) continue;
            Add("script", row.Id, row.Name, row.Evidence, row.Source, null);
            foreach (var name in row.Names ?? []) Add(name.Kind, name.Id, name.Name, name.Evidence, name.Source, name.Scope);
        }
        void Add(string kind, int id, string name, string evidence, string source, string? scope)
        {
            if (!_byKind.TryGetValue(kind, out var rows)) _byKind[kind] = rows = [];
            rows.Add((id, id, new(name, Enum.Parse<GlossaryEvidence>(evidence, true), source), scope));
        }
    }

    internal static string CdFingerprint(LoomScriptDisassembly script) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(string.Join("\n", script.Instructions.Select(i => i.Operation + " " + i.Operands)))));
}

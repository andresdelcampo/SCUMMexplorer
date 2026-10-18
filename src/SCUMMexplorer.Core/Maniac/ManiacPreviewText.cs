using System.Security.Cryptography;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Maniac;

/// <summary>Readable cutscene labels from the reviewed executable's actual glyphs; never used to rewrite script bytes or rendered game text.</summary>
public static class ManiacPreviewText
{
    // Native 128x8 font at executable offset64116; SHA256 of those1024bytes:
    // 336B4DFC32EB17CDF6BA78D9116C7F219605807D9573F185A3CDB0340652813A.
    // Read-only bitmap inspection18October2026 establishes these codepoint substitutions.
    private const string ExecutableHash = "AF7A7F667789E4FCA76F4C195BB2996F622D50E796E6CF01E64E4D0A1E0D477A";

    public static Func<string, string>? For(GameLibrary game)
    {
        var executable = Path.Combine(game.FolderPath, "MANIAC.EXE");
        if (!File.Exists(executable) || Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(executable))) != ExecutableHash) return null;
        return text => new string(text.Select(c => c switch
        {
            '<' => '¡', '=' => 'á', '>' => 'í', '[' => '¿', '\\' => 'è', ']' => 'é',
            '{' => 'ñ', '|' => 'ó', '}' => 'ú', '~' => 'ü', _ => c
        }).ToArray());
    }
}

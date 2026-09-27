using System.Globalization;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Core.Discovery;

/// <summary>
/// Identifies known game-folder layouts and exposes user-facing resources.
/// Format-specific directory parsing belongs in a separate provider.
/// </summary>
public sealed class GameFolderScanner
{
    private static readonly HashSet<string> SoundExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".SOU", ".MIDI", ".MID", ".IMS" };

    public GameLibrary Scan(string folderPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        var fullPath = Path.GetFullPath(folderPath);
        if (!Directory.Exists(fullPath))
            throw new DirectoryNotFoundException($"Game folder not found: {fullPath}");

        var files = Directory.EnumerateFiles(fullPath).ToArray();
        var lflFiles = files.Select(path => (Path: path, Id: ParseLflId(path)))
            .Where(item => item.Id.HasValue)
            .Select(item => (item.Path, Id: item.Id.GetValueOrDefault()))
            .OrderBy(item => item.Id)
            .ToArray();
        var executableNames = files.Select(path => Path.GetFileName(path)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var gameId = executableNames.Contains("LOOM.EXE") ? GameId.Loom
            : executableNames.Contains("INDY3.EXE") ? GameId.IndianaJones3
            : GameId.Unknown;
        var hasIndex = lflFiles.Any(item => item.Id == 0);
        var hasRoomFiles = lflFiles.Length > 1;

        var resources = new List<GameResource>();
        var indexWasParsed = false;
        foreach (var item in lflFiles)
        {
            if (item.Id == 0)
            {
                resources.Add(new GameResource("Game index", ResourceKind.Other, item.Path, 0,
                    "00.LFL is an index resource, not a room."));
                if (hasIndex && hasRoomFiles)
                {
                    try
                    {
                        foreach (var indexed in new LflIndexReader().Read(item.Path))
                        {
                            var fileName = $"{indexed.ContainerFileNumber:00}.LFL";
                            var resourcePath = Path.Combine(fullPath, fileName);
                            if (indexed.Kind == ResourceKind.Room && !File.Exists(resourcePath)) continue;
                            var label = indexed.Kind switch
                            {
                                ResourceKind.Room => $"Room {indexed.ResourceId:00}",
                                ResourceKind.Costume => $"Costume {indexed.ResourceId:00}",
                                ResourceKind.Script => $"Script {indexed.ResourceId:00}",
                                ResourceKind.Sound => $"Sound {indexed.ResourceId:00}",
                                _ => $"{indexed.Kind} {indexed.ResourceId:00}"
                            };
                            var description = gameId == GameId.Loom && indexed.Kind == ResourceKind.Script
                                ? "Indexed by 00.LFL; DOS LOOM old-v3 bytecode has a partial disassembly view."
                                : "Indexed by 00.LFL; resource-specific inspection depends on the supported format readers.";
                            resources.Add(new GameResource(label, indexed.Kind, resourcePath,
                                indexed.ResourceId, description,
                                indexed.ContainerFileNumber, indexed.Offset,
                                indexed.DirectoryEntryOffset, indexed.ContainerNumberOffset));
                        }
                        indexWasParsed = true;
                    }
                    catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException)
                    {
                        // Keep the physical-file fallback useful for incomplete or
                        // unsupported LFL layouts instead of claiming a parse.
                    }
                }
                continue;
            }

            if (!indexWasParsed)
                resources.Add(new GameResource($"Room file {item.Id:00}", ResourceKind.Other, item.Path, item.Id,
                    "Physical LFL file; no supported index was parsed."));
        }

        foreach (var path in files.Where(path => SoundExtensions.Contains(Path.GetExtension(path))))
            resources.Add(new GameResource(Path.GetFileName(path), ResourceKind.Sound, path));

        foreach (var path in files.Where(path => Path.GetExtension(path).Equals(".EXE", StringComparison.OrdinalIgnoreCase)))
            resources.Add(new GameResource(Path.GetFileName(path), ResourceKind.Executable, path));

        var title = gameId switch
        {
            GameId.Loom => "LOOM",
            GameId.IndianaJones3 => "Indiana Jones and the Last Crusade",
            _ => Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        };
        var version = indexWasParsed ? ScummVersion.V3 : ScummVersion.Unknown;
        var note = gameId switch
        {
            GameId.Loom when indexWasParsed =>
                "DOS LOOM profile inferred from its files; the 00.LFL SCUMM v3 signature and resource directories were read.",
            GameId.Loom =>
                "Likely DOS LOOM with the individual-LFL layout. The SCUMM version is not confirmed because the index could not be parsed.",
            GameId.IndianaJones3 when indexWasParsed =>
                "DOS EGA profile inferred from INDY3.EXE and the old-v3 00.LFL resource directory. Room backgrounds are supported; object and script behavior is not yet verified.",
            GameId.IndianaJones3 =>
                "Indiana Jones and the Last Crusade files found. The SCUMM version is not confirmed because no supported old-v3 index was parsed.",
            _ when indexWasParsed =>
                "The old-v3 SCUMM index in 00.LFL was parsed; game identity and resource-specific support are not confirmed.",
            _ => "Folder contents found. Game and SCUMM version are not confirmed yet."
        };

        return new GameLibrary(title, fullPath, version, "DOS (inferred)", resources, note, gameId);
    }

    private static int? ParseLflId(string path)
    {
        if (!Path.GetExtension(path).Equals(".LFL", StringComparison.OrdinalIgnoreCase)) return null;
        var stem = Path.GetFileNameWithoutExtension(path);
        return int.TryParse(stem, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id is >= 0 and <= 999
            ? id : null;
    }
}

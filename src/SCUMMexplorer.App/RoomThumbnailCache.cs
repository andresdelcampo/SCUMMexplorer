using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows.Media.Imaging;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

namespace SCUMMexplorer.App;

/// <summary>
/// Complete room-thumbnail batches, isolated by source path and selected language. A single
/// versioned archive is replaced only after all PNGs and their source/build stamp are ready.
/// Old incremental caches are not read. This cache never decides whether native initialization runs.
/// </summary>
internal static class RoomThumbnailCache
{
    // Harnesses must set this before the first access; the normal user profile is never a test fixture.
    private static readonly string Root = Environment.GetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR") is { Length: > 0 } custom
        ? Path.GetFullPath(custom)
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SCUMMexplorer", "room-cache");
    private const int Version = 3;
    private static readonly object CommitGate = new();
    private sealed record Manifest(int Version, string Stamp, int[] Rooms);

    private static string SourceKey(string gameFolder) =>
        Path.GetFullPath(gameFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToUpperInvariant();

    private static string LanguageKey(string? language) => (language ?? "").Trim().ToUpperInvariant();

    private static string CacheFile(string gameFolder, string? language)
    {
        var key = JsonSerializer.Serialize(new[] { SourceKey(gameFolder), LanguageKey(language) });
        return Path.Combine(Root, $"room-thumbnails-v{Version}-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))) + ".zip");
    }

    private static string? Stamp(string gameFolder, string? language, CancellationToken cancellationToken = default)
    {
        var source = Path.GetFullPath(gameFolder);
        var folder = System.IO.Directory.Exists(source) ? source : File.Exists(source) ? Path.GetDirectoryName(source) : null;
        if (folder is null) return null;
        if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0) return null;
        var lines = new List<string> { $"v{Version}", SourceKey(gameFolder), LanguageKey(language) };
        void Build(Assembly assembly)
        {
            var file = new FileInfo(assembly.Location);
            lines.Add($"build:{assembly.ManifestModule.ModuleVersionId:D}:{(file.Exists ? file.Length : 0)}:{(file.Exists ? file.LastWriteTimeUtc.Ticks : 0)}");
        }
        Build(typeof(GameLibrary).Assembly);
        Build(typeof(RoomThumbnailCache).Assembly);
        // Use the shared cue resolver, including its renamed-image fallback. A cue can name
        // a data image outside its own folder, so directory metadata alone is insufficient.
        var image = System.IO.Directory.Exists(source) ? DiscImage.FindImageIn(source) : source;
        if (image is not null && Path.GetExtension(image).Equals(".cue", StringComparison.OrdinalIgnoreCase))
        {
            var bin = new FileInfo(DiscImage.Open(image).BinPath);
            if ((bin.Attributes & FileAttributes.ReparsePoint) != 0) return null;
            lines.Add($"cue-source:{SourceKey(bin.FullName)}:{bin.Length}:{bin.LastWriteTimeUtc.Ticks}");
        }
        var pending = new Stack<string>();
        pending.Push(folder);
        var cacheRoot = Path.GetFullPath(Root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        while (pending.TryPop(out var current))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var entry in new DirectoryInfo(current).EnumerateFileSystemInfos().OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                // A custom cache inside the source tree must not invalidate itself. Never follow
                // source junctions/symlinks: refusing a cache is safer than stamping an unobserved target.
                if (entry.FullName.Equals(cacheRoot, StringComparison.OrdinalIgnoreCase)) continue;
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0) return null;
                if (entry is DirectoryInfo directory) pending.Push(directory.FullName);
                else if (entry is FileInfo file)
                    lines.Add($"source:{Path.GetRelativePath(folder, file.FullName).ToUpperInvariant()}:{file.Length}:{file.LastWriteTimeUtc.Ticks}");
            }
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', lines))));
    }

    private static string RoomName(int room) => $"room-{room}.png";

    /// <summary>
    /// Commits one nonempty, complete caller-supplied room set. True means the whole batch was
    /// atomically published; false means cancellation, invalid input or a cache/encoding failure.
    /// A previous complete archive survives a failed save. The caller supplies every final room;
    /// TryLoad checks its complete requested room set against the stored manifest.
    /// </summary>
    public static bool SaveCompleted(string gameFolder, IReadOnlyDictionary<int, BitmapSource> thumbnails,
        string? language = null, CancellationToken cancellationToken = default)
    {
        string? staging = null;
        var stagingCreated = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (thumbnails is null || thumbnails.Count == 0) return false;
            var batch = thumbnails.OrderBy(p => p.Key).ToArray();
            if (batch.Length != thumbnails.Count || batch.Select(p => p.Key).Distinct().Count() != batch.Length ||
                batch.Any(p => p.Key < 0 || p.Value is null)) return false;
            var stamp = Stamp(gameFolder, language, cancellationToken);
            if (stamp is null) return false;
            cancellationToken.ThrowIfCancellationRequested();
            System.IO.Directory.CreateDirectory(Root);
            var destination = CacheFile(gameFolder, language);
            staging = Path.Combine(Root, Path.GetFileName(destination) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            using (var file = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stagingCreated = true;
                using var archive = new ZipArchive(file, ZipArchiveMode.Create);
                foreach (var (room, bitmap) in batch)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    // WIC encoders require seeking; a ZIP entry stream cannot seek.
                    using var encoded = new MemoryStream();
                    encoder.Save(encoded);
                    encoded.Position = 0;
                    using var output = archive.CreateEntry(RoomName(room), CompressionLevel.NoCompression).Open();
                    encoded.CopyTo(output);
                    cancellationToken.ThrowIfCancellationRequested();
                }
                using var manifest = archive.CreateEntry("manifest.json", CompressionLevel.NoCompression).Open();
                JsonSerializer.Serialize(manifest, new Manifest(Version, stamp, batch.Select(p => p.Key).ToArray()));
            }
            lock (CommitGate)
            {
                // Source changes during encoding invalidate the new batch, never the old file.
                if (Stamp(gameFolder, language, cancellationToken) != stamp) return false;
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(staging, destination, overwrite: true);
                staging = null;
                return true;
            }
        }
        catch (Exception ex) when (CacheFailure(ex) || ex is OperationCanceledException) { return false; }
        finally
        {
            // Delete only the exact staging file created by this call. Never delete a cache
            // directory recursively or touch an older complete archive on a failed commit.
            if (stagingCreated && staging is not null)
                try { File.Delete(staging); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    /// <summary>The complete requested batch, or null for a missing, stale, corrupt or incomplete cache.</summary>
    public static IReadOnlyDictionary<int, BitmapSource>? TryLoad(string gameFolder, IEnumerable<int> roomIds, string? language = null)
    {
        try
        {
            if (roomIds is null) return null;
            var requested = roomIds.Distinct().Order().ToArray();
            if (requested.Length == 0 || requested.Any(id => id < 0)) return null;
            lock (CommitGate)
            {
                var stamp = Stamp(gameFolder, language);
                if (stamp is null) return null;
                using var file = new FileStream(CacheFile(gameFolder, language), FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
                using var archive = new ZipArchive(file, ZipArchiveMode.Read);
                var manifestEntry = archive.GetEntry("manifest.json");
                if (manifestEntry is null) return null;
                Manifest? manifest;
                using (var input = manifestEntry.Open()) manifest = JsonSerializer.Deserialize<Manifest>(input);
                if (manifest is null || manifest.Version != Version || manifest.Stamp != stamp ||
                    manifest.Rooms is null || !manifest.Rooms.SequenceEqual(requested) || archive.Entries.Count != requested.Length + 1) return null;
                var result = new Dictionary<int, BitmapSource>();
                foreach (var room in requested)
                {
                    var entry = archive.GetEntry(RoomName(room));
                    if (entry is null) return null;
                    using var input = entry.Open();
                    using var encoded = new MemoryStream();
                    input.CopyTo(encoded);
                    encoded.Position = 0;
                    var decoder = new PngBitmapDecoder(encoded, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                    if (decoder.Frames.Count != 1) return null;
                    var bitmap = decoder.Frames[0];
                    bitmap.Freeze();
                    result.Add(room, bitmap);
                }
                return Stamp(gameFolder, language) == stamp ? result : null;
            }
        }
        catch (Exception ex) when (CacheFailure(ex)) { return null; }
    }

    private static bool CacheFailure(Exception ex) => ex is IOException or UnauthorizedAccessException or
        NotSupportedException or FileFormatException or ArgumentException or InvalidOperationException or JsonException;
}

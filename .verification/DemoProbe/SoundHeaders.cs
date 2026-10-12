using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

static class SoundHeaders
{
    public static void Run(string folder)
    {
        var game = new GameFolderScanner().Scan(folder);
        foreach (var r in game.Resources.Where(x => x.Kind == ResourceKind.Sound))
        {
            var bytes = File.ReadAllBytes(r.FilePath);
            for (var i = 0; i < bytes.Length; i++) bytes[i] ^= 0xFF;
            var off = r.Offset ?? 0;
            var n = Math.Min(16, bytes.Length - off);
            Console.WriteLine($"sound {r.ResourceId,3} {Path.GetFileName(r.FilePath)} off {off} isRoland {ScummRolandSong.IsRoland(r)} : {Convert.ToHexString(bytes, off, n)}");
        }
    }
}

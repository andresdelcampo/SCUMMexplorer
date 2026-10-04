using System.Diagnostics;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;
public static class Tr { public static int Cb = -1; public static List<string> Log = new(); public static void W(int r, int v) => Log.Add($"{Cb} {r} {(byte)v}"); }
public static class P {
  public static void Main(string[] args) {
    var oracle = args[1]; var tmp = args[2];
    var sounds = new GameFolderScanner().Scan(args[0]).Resources.Where(r => r.Kind == ResourceKind.Sound && r.ResourceId.HasValue).OrderBy(r => r.ResourceId).ToList();
    foreach (var r in sounds) {
      byte[] data;
      try { data = AdTrace.ReadTrack(r); } catch { continue; }
      bool music = data.Length > 2 && data[2] == 0x80;
      int n = music ? 472 * 150 : 472 * 25;
      var inPath = Path.Combine(tmp, "ad.bin"); var outPath = Path.Combine(tmp, "ad.txt");
      File.WriteAllBytes(inPath, data);
      var p = Process.Start(new ProcessStartInfo(oracle, $"{(music ? "music" : "sfx")} \"{inPath}\" \"{outPath}\" {n}") { UseShellExecute = false })!; p.WaitForExit();
      if (p.ExitCode != 0) { Console.WriteLine($"{r.ResourceId}: oracle exit {p.ExitCode}"); continue; }
      var theirs = File.ReadAllLines(outPath).Where(l => !l.StartsWith("-1 ")).ToList();
      Tr.Log.Clear(); Tr.Cb = -1; string err = "";
      try {
        if (music) { var m = new AdTrace.MusicPlayer(data); for (int cb = 0; cb < n; cb++) { Tr.Cb = cb; m.Tick(); } }
        else { var e = new AdTrace.EffectPlayer(data); for (int cb = 0; cb < n; cb++) { Tr.Cb = cb; e.Tick(); } }
      } catch (Exception ex) { err = ex.GetType().Name + ": " + ex.Message; }
      var ours = Tr.Log.Where(l => !l.StartsWith("-1 ")).ToList();
      int m2 = Math.Min(ours.Count, theirs.Count), first = -1;
      for (int i = 0; i < m2; i++) if (ours[i] != theirs[i]) { first = i; break; }
      Console.WriteLine($"{r.ResourceId}: {(music ? "music" : "sfx")} writes ours={ours.Count} oracle={theirs.Count} firstDiff={first}" + (first >= 0 ? $" ours[{ours[first]}] oracle[{theirs[first]}]" : ours.Count != theirs.Count ? " COUNT" : " IDENTICAL") + (err != "" ? " err=" + err : ""));
    }
  }
}

using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;
using SCUMMexplorer.Core.Rendering;

internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread]
    static void Main(string[] args)
    {
        Directory.CreateDirectory(args[1]);
        var app = new Application(); var window = new MainWindow();
        object? Field(string n) => typeof(MainWindow).GetField(n, Private)!.GetValue(window);
        object? Call(string n, params object?[] values) => typeof(MainWindow).GetMethod(n, Private)!.Invoke(window, values);
        LoomExplorationSession Session() => (LoomExplorationSession)Field("_session")!;
        var report = new List<string>(); int failures = 0;
        void Check(bool ok, string label) { report.Add($"{(ok ? "PASS" : "FAIL")} {label}"); if (!ok) failures++; }
        void Run(int script, params int[] arguments)
        {
            var operands = $"0x{script:X2}, [{string.Join(", ", arguments.Select(n => $"0x{n:X4}"))}]";
            var test = new LoomScriptDisassembly(250,1,0,32,16,
                [new(0,0x0A,"startScript",operands,$"0A {script:X2} " + string.Join(" ",arguments.Select(n=>$"{n & 255:X2} {n >> 8:X2}")) + " FF"),new(5,0,"stopObjectCode","", "00")],null,null,[],[],"Assumed test setup");
            typeof(LoomExplorationSession).GetMethod("RunTopLevelScript",Private)!.Invoke(Session(), [test]);
        }
        byte[] TextPixels() => LoomMessageRenderer.Render(Session().Messages.OnScreen, (LoomCharset)Field("_charset")!).Pixels;
        void Save(string label)
        {
            Call("RefreshRoomPreview"); window.UpdateLayout();
            var element = (FrameworkElement)window.FindName("VerbStripBorder");
            var visual = new DrawingVisual(); using (var dc = visual.RenderOpen()) dc.DrawRectangle(new VisualBrush(element), null, new Rect(0,0,960,168));
            var bitmap = new RenderTargetBitmap(960,168,96,96,PixelFormats.Pbgra32); bitmap.Render(visual);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(Path.Combine(args[1],label+".png")); png.Save(stream);
            report.Add(label+": "+string.Join(" | ",Session().Messages.Drawn.Where(m=>m.Y>=144).Select(m=>$"{m.Source} ({m.X},{m.Y}) glyphs {Convert.ToHexString(m.LineGlyphs.SelectMany(x=>x).ToArray())} erases {m.ErasedBy.Count}")));
        }
        window.ContentRendered += async (_,_) =>
        {
            try
            {
                Call("OpenGameFolder",args[0]);
                var tree=(TreeView)window.FindName("ResourceTree");
                var item=tree.Items.OfType<TreeViewItem>().SelectMany(g=>g.Items.OfType<TreeViewItem>()).Single(i=>i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString()=="Room" && (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag)==2);
                Call("SelectResourceInTree",item.Tag); await Task.Delay(1500);
                Call("StopScenePlayback");
                var game=new GameFolderScanner().Scan(args[0]);
                var images=new LoomRoomObjects().Decode(File.ReadAllBytes(game.Rooms.Single(r=>r.ResourceId==1).FilePath));
                foreach(var image in images.Where(i=>i.Id is >=100 and <=117)) report.Add($"image {image.Id}: {image.Width}x{image.Height}");
                Run(18,2); Run(27,3); Save("before"); var before=TextPixels();
                Run(31,3); Save("during-0");
                for(int frame=0; frame<40; frame++) { Session().AdvanceFrame(); if(frame is 3 or 9 or 19) Save("during-"+(frame+1)); }
                Save("after"); var after=TextPixels();
                int changed=0; for(int y=168;y<200;y++) for(int x=16;x<224;x++) if(before[y*320+x]!=after[y*320+x]) changed++;
                Check(changed==0,$"score and staff survive script 031: {changed} changed text pixels");
                foreach(var note in new[]{1,3,2})
                {
                    Run(27,note); var text=TextPixels(); Run(31,note);
                    for(int frame=0;frame<40;frame++) Session().AdvanceFrame();
                    var played=TextPixels(); int lost=0;
                    for(int y=168;y<188;y++) for(int x=16;x<224;x++) if(text[y*320+x]!=LoomMessageRenderer.Transparent && played[y*320+x]==LoomMessageRenderer.Transparent) lost++;
                    Check(lost==0,$"note {note}: playback loses {lost} score/staff pixels"); Save("after-note-"+note);
                }
                var staff=TextPixels();
                for(int n=0;n<16;n++) { Run(28); Run(27,n%3+1); Run(31,n%3+1); for(int f=0;f<40;f++) Session().AdvanceFrame(); }
                var repeated=TextPixels(); int missing=0;
                for(int y=177;y<188;y++) for(int x=16;x<224;x++) if(staff[y*320+x]!=LoomMessageRenderer.Transparent && repeated[y*320+x]==LoomMessageRenderer.Transparent) missing++;
                Check(missing==0,$"repeated playback retains the score's staff: {missing} lost pixels"); Save("after-repeated");
                report.Add("No physical input. Script 018/027/031 invoked through test seam. Standard score glyph at y169; practice score at y188 is a separate case.");
            }
            catch(Exception ex) {report.Add(ex.ToString());failures++;}
            finally {report.Add($"Failures: {failures}");File.WriteAllLines(Path.Combine(args[1],"score-check.txt"),report);window.Close();app.Shutdown();Environment.ExitCode=failures==0?0:1;}
        };
        app.Run(window);
    }
}

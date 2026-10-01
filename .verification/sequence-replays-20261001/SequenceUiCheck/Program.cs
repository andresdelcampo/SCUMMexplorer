using System.Collections;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

internal static class Program
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    [STAThread] static void Main(string[] args)
    {
        Directory.CreateDirectory(args[1]); var app=new Application(); var w=new MainWindow();
        var report=new List<string>(); int failures=0;
        object? Field(string n)=>typeof(MainWindow).GetField(n,Private)!.GetValue(w);
        object? Call(string n,params object?[] v)=>typeof(MainWindow).GetMethod(n,Private)!.Invoke(w,v);
        LoomExplorationSession Session()=>(LoomExplorationSession)Field("_session")!;
        object Scene(string id)=>((IEnumerable)Call("AllCutscenes")!).Cast<object>().Single(s=>(string)s.GetType().GetProperty("Id")!.GetValue(s)! == id);
        void Check(bool ok,string text){report.Add($"{(ok?"PASS":"FAIL")} {text}");if(!ok)failures++;}
        void Capture(string name)
        {
            w.UpdateLayout();var b=new RenderTargetBitmap((int)w.ActualWidth,(int)w.ActualHeight,96,96,PixelFormats.Pbgra32);b.Render(w);
            var enc=new PngBitmapEncoder();enc.Frames.Add(BitmapFrame.Create(b));using var f=File.Create(Path.Combine(args[1],name+".png"));enc.Save(f);
        }
        w.ContentRendered+=async (_,_)=>
        {
            async Task Wait(Func<bool> condition,int limit=8000){for(int i=0;i<limit&&!condition();i++)await Task.Delay(50);if(!condition())throw new TimeoutException();}
            async Task VisibleRoom(int id){await Wait(()=>((GameResource?)Field("_currentRoomResource"))?.ResourceId==id&&((Image)w.FindName("RoomPreview")).IsVisible);await Task.Delay(250);}
            async Task Pick(int id){Call("SelectResourceInTree",((GameLibrary)Field("_game")!).Rooms.Single(r=>r.ResourceId==id));await VisibleRoom(id);}
            try
            {
                Call("OpenGameFolder",args[0]);await Pick(10);
                foreach(var route in args.Length > 2 ? Array.Empty<(string,int,int,string)>() : new[]{("sequence-052-053",53,12,"+0x02AC"),("sequence-086-087",87,38,"+0x09B9")})
                {
                    Call("PlayKnownCutscene",Scene(route.Item1));var same=Session();
                    Check(Field("_replaySequence") is not null&&!((Button)w.FindName("CutsceneMenuButton")).IsEnabled,"sequence locks starts through its prelude");
                    Call("PauseScenePlayback");int traces=Session().SessionTrace.Count;
                    await Task.Delay(350);Check(traces==Session().SessionTrace.Count&&(bool)Field("_playbackPaused")!,"paused sequence advances neither frames nor later inputs");Capture(route.Item1+"-paused");Call("ResumeScenePlayback");
                    await Wait(()=>Session().SessionTrace.Any(t=>t.StartsWith($"cutscene at Script {route.Item2} ")));
                    Check(ReferenceEquals(same,Session()),"continuation uses the same live session");Capture(route.Item1+"-continuation");
                    await Wait(()=>Field("_playback") is null);await VisibleRoom(route.Item3);
                    Check(Session().SessionTrace.Any(t=>t.StartsWith($"endCutscene at Script {route.Item2} {route.Item4}"))&&!Session().InCutscene&&Field("_replaySequence") is null,"full normal-clock sequence reaches source end and visible final room");
                    Check(((Button)w.FindName("CutsceneMenuButton")).IsEnabled&&((ComboBox)w.FindName("SoundModeCombo")).IsEnabled,"completed sequence restores controls");Capture(route.Item1+"-completed");
                }
                Call("PlayKnownCutscene",Scene("sequence-052-053"));Call("PauseScenePlayback");await Pick(16);
                Check(Field("_replaySequence") is null&&!Session().InCutscene&&!((Button)w.FindName("PlaybackPauseButton")).IsEnabled,"room departure cancels paused sequence and pending inputs; idle arrival clock is allowed");
                Call("PlayKnownCutscene",Scene("sequence-052-053"));await Wait(()=>Session().CutsceneSkippable);Call("SkipCutscene");
                Check(Field("_replaySequence") is null,"whole-scene skip cancels remaining sequence inputs");await Pick(16);
                report.Add("Visible MainWindow, entire sequences on normal Dispatcher clock, synthetic controls. No physical input, original-game fidelity or independent listening claim.");
            }
            catch(Exception ex){report.Add(ex.ToString());failures++;}
            finally{report.Add($"Failures: {failures}");File.WriteAllLines(Path.Combine(args[1],"sequence-ui-check.txt"),report);w.Close();app.Shutdown();Environment.ExitCode=failures==0?0:1;}
        };
        app.Run(w);
    }
}

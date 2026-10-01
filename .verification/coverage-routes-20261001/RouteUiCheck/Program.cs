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
        int Prop(object o,string n)=>(int)o.GetType().GetProperty(n)!.GetValue(o)!;
        LoomExplorationSession Session()=>(LoomExplorationSession)Field("_session")!;
        void Check(bool ok,string text){report.Add($"{(ok?"PASS":"FAIL")} {text}");if(!ok)failures++;}
        void Capture(string name)
        {
            w.UpdateLayout();var b=new RenderTargetBitmap((int)w.ActualWidth,(int)w.ActualHeight,96,96,PixelFormats.Pbgra32);b.Render(w);
            var enc=new PngBitmapEncoder();enc.Frames.Add(BitmapFrame.Create(b));using var f=File.Create(Path.Combine(args[1],name+".png"));enc.Save(f);
        }
        w.ContentRendered+=async (_,_)=>
        {
            async Task Wait(Func<bool> condition,int limit=3000){for(int i=0;i<limit&&!condition();i++)await Task.Delay(50);if(!condition())throw new TimeoutException();}
            async Task VisibleRoom(int id){await Wait(()=>((GameResource?)Field("_currentRoomResource"))?.ResourceId==id&&((Image)w.FindName("RoomPreview")).IsVisible);await Task.Delay(250);}
            async Task Prelude(int room,int objectId)
            {
                Call("SelectResourceInTree",((GameLibrary)Field("_game")!).Rooms.Single(r=>r.ResourceId==room));await VisibleRoom(room);
                typeof(MainWindow).GetField("_playInstantly",Private)!.SetValue(w,true);
                var scene=((IEnumerable)Call("AllCutscenes")!).Cast<object>().First(s=>Prop(s,"RoomId")==room&&Prop(s,"ObjectId")==objectId&&Prop(s,"VerbId")==56);
                Call("PlayKnownCutscene",scene);await Wait(()=>Field("_playback") is null);await VisibleRoom(Session().Snapshot.RoomId!.Value);
                typeof(MainWindow).GetField("_playInstantly",Private)!.SetValue(w,false);
            }
            try
            {
                Call("OpenGameFolder",args[0]);await Prelude(10,295);
                Check(Session().Snapshot.RoomId==13&&!Session().InCutscene,"052 prelude ends in visible Room13, with the same live session");
                var watcherSession=Session(); Call("HandleRoomPoint",112,131,false);
                await Wait(()=>Session().SessionTrace.Any(t=>t.StartsWith("cutscene at Script 53 ")));
                Check(Field("_playback") is not null,"floor input reaches watcher053 on the normal WPF clock");Capture("watcher-playing");
                await Wait(()=>Field("_playback") is null);await VisibleRoom(12);
                Check(ReferenceEquals(watcherSession,Session())&&Session().SessionTrace.Any(t=>t.StartsWith("endCutscene at Script 53 +0x02AC"))&&!Session().InCutscene,
                    "watcher053 reaches its matching end and Room12 without replacing state");
                Check(((Button)w.FindName("CutsceneMenuButton")).IsEnabled&&((ComboBox)w.FindName("SoundModeCombo")).IsEnabled,"watcher route restores idle controls");Capture("watcher-completed");
                await Prelude(36,599);
                Check(Session().Snapshot.RoomId==38&&!Session().InCutscene,"086 prelude ends in visible Room38");
                var actorSession=Session();
                Call("SendInputToGame",(Func<LoomObjectActionResult>)(()=>Session().ActivateObjectVerb(610,54)),"Diagnostic direct look at object610",null);
                await Wait(()=>Session().SessionTrace.Any(t=>t.StartsWith("cutscene at Script 87 ")));
                Check(Field("_playback") is not null,"direct look reaches087 on the normal WPF clock");Capture("actor-box-playing");
                await Wait(()=>Field("_playback") is null);await VisibleRoom(38);
                Check(ReferenceEquals(actorSession,Session())&&Session().SessionTrace.Any(t=>t.StartsWith("endCutscene at Script 87 +0x09B9"))&&!Session().InCutscene,
                    "087 reaches matching end in the source-established086 session");
                Check(((Button)w.FindName("CutsceneMenuButton")).IsEnabled&&((ComboBox)w.FindName("SoundModeCombo")).IsEnabled,"actor route restores idle controls");Capture("actor-box-completed");
                report.Add("Visible MainWindow; instant existing preludes, normal Dispatcher clock for053/087; synthetic floor handler and explicitly direct look, no physical mouse/draft cast or independent listening. No production recipe promotion.");
            }
            catch(Exception ex){report.Add(ex.ToString());failures++;}
            finally{report.Add($"Failures: {failures}");File.WriteAllLines(Path.Combine(args[1],"route-ui-check.txt"),report);w.Close();app.Shutdown();Environment.ExitCode=failures==0?0:1;}
        };
        app.Run(w);
    }
}

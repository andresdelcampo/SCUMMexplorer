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
        Directory.CreateDirectory(args[1]);var app=new Application();var w=new MainWindow();var report=new List<string>();int failures=0;
        object? Field(string n)=>typeof(MainWindow).GetField(n,Private)!.GetValue(w);
        object? Call(string n,params object?[] v)=>typeof(MainWindow).GetMethod(n,Private)!.Invoke(w,v);
        LoomExplorationSession Session()=>(LoomExplorationSession)Field("_session")!;
        void Check(bool ok,string text){report.Add($"{(ok?"PASS":"FAIL")} {text}");if(!ok)failures++;}
        void Capture(string name){w.UpdateLayout();var b=new RenderTargetBitmap((int)w.ActualWidth,(int)w.ActualHeight,96,96,PixelFormats.Pbgra32);b.Render(w);var enc=new PngBitmapEncoder();enc.Frames.Add(BitmapFrame.Create(b));using var f=File.Create(Path.Combine(args[1],name+".png"));enc.Save(f);}
        w.ContentRendered+=async (_,_)=>
        {
            async Task Wait(Func<bool> c,int limit=8000){for(int i=0;i<limit&&!c();i++)await Task.Delay(50);if(!c())throw new TimeoutException();}
            try
            {
                Call("OpenGameFolder",args[0]);Call("SelectResourceInTree",((GameLibrary)Field("_game")!).Rooms.Single(r=>r.ResourceId==41));
                await Wait(()=>((GameResource?)Field("_currentRoomResource"))?.ResourceId==41&&((Image)w.FindName("RoomPreview")).IsVisible);await Task.Delay(250);
                foreach(var id in args.Length > 2 ? new[]{"Room 24 object 432 Verb 0x36"} : new[]{"Room 41 object 633 Verb 0x41","Room 41 object 633 Verb 0x4B","Room 24 object 432 Verb 0x36"})
                {
                    bool normal=id.EndsWith("0x41");typeof(MainWindow).GetField("_playInstantly",Private)!.SetValue(w,!normal);
                    if(id.StartsWith("Room 24"))
                    {
                        // Instant mode skips the normal frame that asks the view to show
                        // a different start room when this recipe never leaves that room.
                        Call("SelectResourceInTree",((GameLibrary)Field("_game")!).Rooms.Single(r=>r.ResourceId==24));
                        await Wait(()=>((GameResource?)Field("_currentRoomResource"))?.ResourceId==24&&((Image)w.FindName("RoomPreview")).IsVisible);await Task.Delay(250);
                    }
                    var scene=((IEnumerable)Call("AllCutscenes")!).Cast<object>().Single(s=>(string)s.GetType().GetProperty("Id")!.GetValue(s)! == id);
                    Call("PlayKnownCutscene",scene);
                    if(normal){Check(Field("_playback") is not null,"Room41 response runs visibly on normal clock");await Task.Delay(1500);Capture("room41-child-playing");}
                    await Wait(()=>(bool)typeof(MainWindow).GetProperty("CanStartCutscene",Private)!.GetValue(w)!);
                    await Wait(()=>((GameResource?)Field("_currentRoomResource"))?.ResourceId==Session().Snapshot.RoomId&&((Image)w.FindName("RoomPreview")).IsVisible);await Task.Delay(250);
                    var trace=Session().SessionTrace.Where(t=>t.StartsWith("cutscene at ")||t.StartsWith("endCutscene at ")).ToArray();
                    Check(trace.Count(t=>t.StartsWith("cutscene at "))==trace.Count(t=>t.StartsWith("endCutscene at "))&&!Session().InCutscene,id+" has balanced scopes");
                    if(id.StartsWith("Room 41")) Check(Array.FindIndex(trace,t=>t.StartsWith("endCutscene at Room 41 local script 213 +0x00A9"))<Array.FindIndex(trace,t=>t.StartsWith("cutscene at Room 41 local script 204 +0x0006")),"child213 ends before frozen parent204 opens its scope");
                    Check(((Button)w.FindName("CutsceneMenuButton")).IsEnabled&&((ComboBox)w.FindName("SoundModeCombo")).IsEnabled,"completed response restores controls");Capture(id.StartsWith("Room 24")?"room24-completed":normal?"room41-response41-completed":"room41-response4b-completed");
                }
            }
            catch(Exception ex){report.Add(ex.ToString());failures++;}
            finally{report.Add(args.Length > 2 ? "Visible Room24-only instant check; normal Room41 evidence retained separately." : "Visible WPF; first Room41 response normal clock, other two instant. Synthetic controls, no actual draft casting or original-game input claim.");report.Add($"Failures: {failures}");File.WriteAllLines(Path.Combine(args[1],"scope-ui-check.txt"),report);w.Close();app.Shutdown();Environment.ExitCode=failures==0?0:1;}
        };app.Run(w);
    }
}

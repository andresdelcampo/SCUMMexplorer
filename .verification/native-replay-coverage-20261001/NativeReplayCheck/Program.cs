using System.Collections;
using System.IO;
using System.Reflection;
using System.Text.Json;
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
        Directory.CreateDirectory(args[1]); var app=new Application(); var w=new MainWindow(); var report=new List<string>(); int failures=0;
        object? Field(string n)=>typeof(MainWindow).GetField(n,Private)!.GetValue(w);
        object? Call(string n,params object?[] v)=>typeof(MainWindow).GetMethod(n,Private)!.Invoke(w,v);
        T Prop<T>(object o,string n)=>(T)o.GetType().GetProperty(n)!.GetValue(o)!;
        void Check(bool ok,string text){report.Add($"{(ok?"PASS":"FAIL")} {text}");if(!ok)failures++;}
        w.ContentRendered+=async (_,_)=>
        {
            async Task Wait(Func<bool> condition){for(int i=0;i<600&&!condition();i++)await Task.Delay(50);if(!condition())throw new TimeoutException();}
            async Task Pick(int id){Call("SelectResourceInTree",((GameLibrary)Field("_game")!).Rooms.Single(r=>r.ResourceId==id));await Wait(()=>((GameResource?)Field("_currentRoomResource"))?.ResourceId==id&&((Image)w.FindName("RoomPreview")).IsVisible);await Task.Delay(200);}
            try
            {
                Call("OpenGameFolder",args[0]); await Pick(16);
                var recipes=JsonDocument.Parse(File.ReadAllText(args[2])).RootElement.EnumerateArray().ToArray();
                var all=((IEnumerable)Call("AllCutscenes")!).Cast<object>().ToDictionary(s=>Prop<string>(s,"Id"));
                typeof(MainWindow).GetField("_playInstantly",Private)!.SetValue(w,true);
                foreach(var recipe in recipes)
                {
                    await Pick(recipe.GetProperty("room").GetInt32());
                    var id=recipe.GetProperty("id").GetString()!;var scene=all[id];
                    Check(Prop<string>(scene,"Title")==recipe.GetProperty("title").GetString(),id+": shared resolved source title");
                    Call("PlayKnownCutscene",scene);
                    await Wait(()=>Field("_playback") is null);
                    var session=(LoomExplorationSession)Field("_session")!;
                    await Wait(()=>((GameResource?)Field("_currentRoomResource"))?.ResourceId==session.Snapshot.RoomId&&((Image)w.FindName("RoomPreview")).IsVisible);
                    var sources=recipe.GetProperty("source").GetString()!.Split('\n').Where(t=>t.StartsWith("cutscene at ")||t.StartsWith("endCutscene at ")).ToArray();
                    Check(!session.InCutscene&&session.Messages.HaveMsg==0&&!session.EgoMoving&&!session.CameraMoving&&
                        sources.All(s=>session.SessionTrace.Contains(s)),id+": matching source boundaries and settled WPF replay");
                    Check(((Button)w.FindName("CutsceneMenuButton")).IsEnabled&&((ComboBox)w.FindName("SoundModeCombo")).IsEnabled,
                        id+": idle controls restored");
                    if(recipe.GetProperty("rooms").GetArrayLength()>1)
                    {
                        w.UpdateLayout();var b=new RenderTargetBitmap((int)w.ActualWidth,(int)w.ActualHeight,96,96,PixelFormats.Pbgra32);b.Render(w);
                        var enc=new PngBitmapEncoder();enc.Frames.Add(BitmapFrame.Create(b));using var f=File.Create(Path.Combine(args[1],$"completed-{recipe.GetProperty("room").GetInt32()}-{recipe.GetProperty("objectId").GetInt32()}.png"));enc.Save(f);
                    }
                }
                report.Add($"{recipes.Length} new recipes, real MainWindow instant batching; independent normal-clock sound/pause/mute check recorded separately. Synthetic UI calls; no physical input or independent listening.");
            }
            catch(Exception ex){report.Add(ex.ToString());failures++;}
            finally{report.Add($"Failures: {failures}");File.WriteAllLines(Path.Combine(args[1],"native-replay-check.txt"),report);w.Close();app.Shutdown();Environment.ExitCode=failures==0?0:1;}
        };
        app.Run(w);
    }
}

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
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread] static void Main(string[] args)
    {
        Directory.CreateDirectory(args[1]); var app=new Application(); var w=new MainWindow(); var report=new List<string>(); int failures=0;
        int total=args.Length>2?int.Parse(args[2]):291, scenes=args.Length>3?int.Parse(args[3]):75,
            reactions=args.Length>4?int.Parse(args[4]):216, gaps=args.Length>5?int.Parse(args[5]):79;
        object? Field(string n)=>typeof(MainWindow).GetField(n,Private)!.GetValue(w);
        object? Call(string n,params object?[] v)=>typeof(MainWindow).GetMethod(n,Private)!.Invoke(w,v);
        T Prop<T>(object o,string n)=>(T)o.GetType().GetProperty(n)!.GetValue(o)!;
        T Find<T>(string n)=>(T)w.FindName(n);
        void Check(bool ok,string text){report.Add($"{(ok?"PASS":"FAIL")} {text}");if(!ok)failures++;}
        void Capture(Window window,string name){window.UpdateLayout();var b=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);b.Render(window);var enc=new PngBitmapEncoder();enc.Frames.Add(BitmapFrame.Create(b));using var f=File.Create(Path.Combine(args[1],name+".png"));enc.Save(f);}
        w.ContentRendered+=async (_,_)=>
        {
            async Task Wait(Func<bool> condition){for(int i=0;i<200&&!condition();i++)await Task.Delay(50);if(!condition())throw new TimeoutException();}
            async Task Pick(int id){Call("SelectResourceInTree",((GameLibrary)Field("_game")!).Rooms.Single(r=>r.ResourceId==id));await Wait(()=>((GameResource?)Field("_currentRoomResource"))?.ResourceId==id);await Task.Delay(200);}
            try
            {
                Call("OpenGameFolder",args[0]);await Pick(16);
                var all=((IEnumerable)Call("AllCutscenes")!).Cast<object>().ToArray();
                Check(all.Length+1==total,$"expanded catalog has {total} starts including opening");
                var picker=(Window)Call("BuildCutscenePicker")!;
                picker.ContentRendered+=(_,_)=>
                {
                    try
                    {
                        var filter=(ComboBox)picker.FindName("CategoryFilter");var search=(TextBox)picker.FindName("SearchBox");var list=(ListBox)picker.FindName("SceneList");var play=(Button)picker.FindName("PlayButton");
                        Check(list.Items.Count==scenes,$"Scenes lists {scenes} starts globally");Capture(picker,"picker-scenes");
                        filter.SelectedIndex=1;Check(list.Items.Count==reactions,"dialogue/reactions separated from scenes");
                        filter.SelectedIndex=3;Check(list.Items.Count==gaps&&!play.IsEnabled,$"{gaps} owners with outstanding sites are visible and cannot replay an invented start");Capture(picker,"picker-gaps");
                        filter.SelectedIndex=2;search.Text="Room 02 · Draft response 1 at cielo";
                        Check(list.Items.Count==1&&play.IsEnabled,"search finds sky response whose script 049 was absent from manual menu");
                        play.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                    }
                    catch(Exception ex){report.Add(ex.ToString());failures++;picker.Close();}
                };
                if(picker.ShowDialog()==true)((Action?)picker.GetType().GetProperty("SelectedPlay",Private)!.GetValue(picker))?.Invoke();
                await Wait(()=>((LoomExplorationSession)Field("_session")!).InCutscene);
                await Wait(()=>((FrameworkElement)w.FindName("RoomPreview")).IsVisible &&
                    ((GameResource?)Field("_currentRoomResource"))?.ResourceId == ((LoomExplorationSession)Field("_session")!).Snapshot.RoomId);
                Check(!Find<Button>("CutsceneMenuButton").IsEnabled&&!Find<ComboBox>("SoundModeCombo").IsEnabled,"new replay starts lock picker and sound during the real clock");
                Call("PauseScenePlayback");Capture(w,"sky-replay-paused");Check(!Find<ComboBox>("SoundModeCombo").IsEnabled,"new replay pause keeps sound locked");await Pick(16);
                Check(!((LoomExplorationSession)Field("_session")!).InCutscene&&!Find<Button>("PlaybackPauseButton").IsEnabled,"new replay departure cancels the scene and keeps idle Pause disabled");
                var gallery=(IDictionary)Call("GalleryCutscenes")!;Check(gallery.Contains(49),"unique reached story script shares its descriptor with Game Texts");
                typeof(MainWindow).GetField("_playInstantly",Private)!.SetValue(w,true);
                foreach(var tuple in new[]{(2,156,0x3A),(13,325,0x41),(18,371,0x36),(26,464,0x4D),(49,707,0x36),(62,808,0x3C)})
                {
                    await Pick(16);
                    var scene=all.Single(s=>Prop<int>(s,"RoomId")==tuple.Item1&&Prop<int>(s,"ObjectId")==tuple.Item2&&Prop<int>(s,"VerbId")==tuple.Item3);
                    Call("PlayKnownCutscene",scene);await Task.Delay(450);
                    var session=(LoomExplorationSession)Field("_session")!;
                    Check(!session.InCutscene&&Find<Button>("CutsceneMenuButton").IsEnabled&&Find<ComboBox>("SoundModeCombo").IsEnabled,$"{Prop<string>(scene,"Title")}: WPF playback completes with idle controls restored");
                    Check(session.SessionTrace.Any(t=>t.StartsWith("endCutscene at "))&&((Image)w.FindName("RoomPreview")).Source is not null,"completed scene has reached boundary and visible room bitmap");
                }
                report.Add("Real WPF modal picker, search/filter/selection/Play, visible layout, live clock and six instant replays. Synthetic input; no physical input or independent listening.");
            }
            catch(Exception ex){report.Add(ex.ToString());failures++;}
            finally{report.Add($"Failures: {failures}");File.WriteAllLines(Path.Combine(args[1],"replay-check.txt"),report);w.Close();app.Shutdown();Environment.ExitCode=failures==0?0:1;}
        };
        app.Run(w);
    }
}

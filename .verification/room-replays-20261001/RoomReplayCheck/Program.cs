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
        T Prop<T>(object o,string n)=>(T)o.GetType().GetProperty(n)!.GetValue(o)!;
        T Find<T>(string n)=>(T)w.FindName(n);
        object[] Items(int room)=>((IEnumerable)Call("RoomCutsceneItems",room)!).Cast<object>().ToArray();
        void Check(bool ok,string text){report.Add($"{(ok?"PASS":"FAIL")} {text}");if(!ok)failures++;}
        void Capture(Window window,string name){window.UpdateLayout();var b=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);b.Render(window);var enc=new PngBitmapEncoder();enc.Frames.Add(BitmapFrame.Create(b));using var f=File.Create(Path.Combine(args[1],name+".png"));enc.Save(f);}
        w.ContentRendered+=async (_,_)=>
        {
            async Task Wait(Func<bool> condition){for(int i=0;i<250&&!condition();i++)await Task.Delay(50);if(!condition())throw new TimeoutException();}
            async Task Pick(int id){Call("SelectResourceInTree",((GameLibrary)Field("_game")!).Rooms.Single(r=>r.ResourceId==id));await Wait(()=>((GameResource?)Field("_currentRoomResource"))?.ResourceId==id && Find<Image>("RoomPreview").IsVisible);await Task.Delay(250);}
            try
            {
                Call("OpenGameFolder",args[0]);await Pick(2);
                var rooms=((GameLibrary)Field("_game")!).Rooms.Select(r=>r.ResourceId!.Value).ToArray();
                var memberships=((IEnumerable)Prop<object>(Field("_replayCatalog")!,"Membership")).Cast<object>().ToArray();
                Check(memberships.Length==291,"all 291 starts share generated validated membership");
                Check(rooms.All(id=>Items(id).Length==memberships.Count(m=>Prop<int>(m,"StartRoom")==id||Prop<int[]>(m,"Rooms").Contains(id))),"every room count follows shared metadata, including newly generated entries");
                foreach(var pair in new[]{("opening",new[]{1,2,53,69}),("Room 08 object 255 Verb 0x38",new[]{3,8,9,11,54,67}),("Room 10 object 295 Verb 0x38",new[]{10,11,13}),("Room 36 object 599 Verb 0x38",new[]{36,37,38,58})})
                    Check(memberships.Any(m=>Prop<string>(m,"Id")==pair.Item1 && Prop<int[]>(m,"Rooms").SequenceEqual(pair.Item2)),pair.Item1+": fresh executed multi-room evidence loaded");
                var none=rooms.First(id=>Items(id).Length==0);var one=rooms.First(id=>Items(id).Length==1);
                await Pick(none);Check(!Find<Button>("RoomCutsceneButton").IsVisible&&Find<Button>("CutsceneMenuButton").IsEnabled,$"Room {none:00}: no room button, global remains enabled");Capture(w,"zero-matches");
                await Pick(one);Check(Find<Button>("RoomCutsceneButton").IsVisible&&Find<Button>("RoomCutsceneButton").IsEnabled,$"Room {one:00}: one match has room access");Capture(w,"one-match");
                var prior=Field("_session");Find<Button>("RoomCutsceneButton").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Check(!ReferenceEquals(prior,Field("_session"))&&!app.Windows.OfType<CutscenePickerWindow>().Any(),"single room match starts directly with a fresh session, no picker");
                await Wait(()=>((LoomExplorationSession)Field("_session")!).BootRunning||((LoomExplorationSession)Field("_session")!).InCutscene);
                Check(!Find<Button>("RoomCutsceneButton").IsEnabled&&!Find<Button>("CutsceneMenuButton").IsEnabled,"room and global starts share playing guard");
                Call("PauseScenePlayback");Check(!Find<Button>("RoomCutsceneButton").IsEnabled&&!Find<ComboBox>("SoundModeCombo").IsEnabled,"paused room replay locks starts and sound");
                prior=Field("_session");Call("RoomCutscene_Click",null,new RoutedEventArgs());Check(ReferenceEquals(prior,Field("_session")),"room handler refuses repeated start during pause");
                await Pick(2);Check(!((LoomExplorationSession)Field("_session")!).BootRunning&&!Find<Button>("PlaybackPauseButton").IsEnabled,"departure cancels opening and pause clock");
                var expected=Items(2).Select(s=>Prop<string>(s,"Title")).ToArray();
                _ = w.Dispatcher.BeginInvoke(new Action(async ()=>
                {
                    CutscenePickerWindow? picker=null;
                    try
                    {
                        await Wait(()=>app.Windows.OfType<CutscenePickerWindow>().Any());picker=app.Windows.OfType<CutscenePickerWindow>().Single();await Task.Delay(100);
                        var list=(ListBox)picker.FindName("SceneList");var filter=(ComboBox)picker.FindName("CategoryFilter");var search=(TextBox)picker.FindName("SearchBox");
                        Check(list.Items.Cast<object>().Select(s=>Prop<string>(s,"Title")).SequenceEqual(expected),"multiple matches open room-filtered picker with shared global titles");
                        Check(((TextBlock)picker.FindName("PickerScope")).Text.Contains("Room 02")&&((TextBox)picker.FindName("SourceDetails")).Text.Contains("fresh session"),"room scope and fresh-session start note visible");
                        Capture(picker,"room-picker");filter.SelectedIndex=1;Check(list.Items.Count>0,"room picker retains dialogue/reaction category");
                        filter.SelectedIndex=2;search.Text="Room 02 · Draft response 1 at cielo";Check(list.Items.Count==1,"new source-derived entry searchable through room access without UI-specific mapping");
                        ((Button)picker.FindName("PlayButton")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                    }
                    catch(Exception ex){report.Add(ex.ToString());failures++;picker?.Close();}
                }));
                Find<Button>("RoomCutsceneButton").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                await Wait(()=>((LoomExplorationSession)Field("_session")!).InCutscene);
                Call("PauseScenePlayback");await Wait(()=>((GameResource?)Field("_currentRoomResource"))?.ResourceId==((LoomExplorationSession)Field("_session")!).Snapshot.RoomId&&Find<Image>("RoomPreview").IsVisible);Capture(w,"room-replay-paused");
                Check(!Find<Button>("RoomCutsceneButton").IsEnabled&&!Find<ComboBox>("SoundModeCombo").IsEnabled,"new room replay shares pause and sound locks");
                await Pick(none);Check(!((LoomExplorationSession)Field("_session")!).InCutscene&&!Find<Button>("PlaybackPauseButton").IsEnabled&&Find<Button>("CutsceneMenuButton").IsEnabled,"scene departure restores idle controls and global access");
                Check(!Find<Button>("GameTextsButton").IsVisible,"Game Texts stays Advanced-only");
                report.Add("Visible WPF room button, actual modal handler, shared category/search/Play, normal clock and pause/departure. Synthetic UI events; no physical input or independent listening.");
            }
            catch(Exception ex){report.Add(ex.ToString());failures++;}
            finally{report.Add($"Failures: {failures}");File.WriteAllLines(Path.Combine(args[1],"room-replays-check.txt"),report);w.Close();app.Shutdown();Environment.ExitCode=failures==0?0:1;}
        };
        app.Run(w);
    }
}

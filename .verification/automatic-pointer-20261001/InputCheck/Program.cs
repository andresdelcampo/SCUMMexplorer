using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
        Directory.CreateDirectory(args[1]); var app=new Application();var w=new MainWindow();var report=new List<string>();int failures=0;
        object? Field(string n)=>typeof(MainWindow).GetField(n,Private)!.GetValue(w);
        object? Call(string n,params object?[] values)=>typeof(MainWindow).GetMethod(n,Private)!.Invoke(w,values);
        T Find<T>(string n)=>(T)w.FindName(n);
        LoomExplorationSession Session()=>(LoomExplorationSession)Field("_session")!;
        void Check(bool ok,string text){report.Add($"{(ok?"PASS":"FAIL")} {text}");if(!ok)failures++;}
        w.ContentRendered+=async (_,_)=>
        {
            async Task Wait(Func<bool> condition){for(int i=0;i<200&&!condition();i++)await Task.Delay(50);if(!condition())throw new TimeoutException();}
            async Task Toggle(bool value){var advanced=Find<CheckBox>("AdvancedModeToggle");advanced.IsChecked=value;advanced.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));await Task.Delay(200);w.UpdateLayout();}
            async Task Pick(int id){Call("SelectResourceInTree",((GameLibrary)Field("_game")!).Rooms.Single(r=>r.ResourceId==id));await Wait(()=>((GameResource?)Field("_currentRoomResource"))?.ResourceId==id&&Find<Image>("RoomPreview").IsVisible);await Task.Delay(200);}
            async Task Clicks(string label)
            {
                Check(Find<CheckBox>("SessionModeToggle").IsChecked==true,label+": interactive enabled");
                var viewport=Find<FrameworkElement>("RoomViewport");var hit=w.InputHitTest(viewport.TranslatePoint(new Point(viewport.ActualWidth/2,viewport.ActualHeight/2),w));
                Check(ReferenceEquals(hit,Find<Image>("RoomPreview")),label+": visible hit test reaches room image");
                int before=Session().SessionTrace.Count;Call("HandleRoomPoint",700,40,false);await Task.Delay(100);
                Check(Session().SessionTrace.Skip(before).Any(s=>s.Contains("runs script 5")),label+": room click reaches game input");
                var setup=new LoomScriptDisassembly(250,1,0,32,16,[new(0,0x0A,"startScript","0x12, [0x0002]","0A 12 02 00 FF"),new(5,0,"stopObjectCode","","00")],null,null,[],[],"Assumed distaff setup");
                typeof(LoomExplorationSession).GetMethod("RunTopLevelScript",Private)!.Invoke(Session(),[setup]);Call("RefreshRoomPreview");w.UpdateLayout();
                var image=Find<Image>("VerbStripImage");before=Session().SessionTrace.Count;
                var sent=(bool)Call("ClickVerbStrip",new Point(73.5*image.ActualWidth/320,1.5*image.ActualHeight/56))!;
                Check(sent&&Session().SessionTrace.Count>before,label+": visible note click reaches game input");
            }
            try
            {
                typeof(MainWindow).GetField("_playInstantly",Private)!.SetValue(w,true);Call("OpenGameFolder",args[0]);await Pick(2);
                Check(w.FindName("GamePointerToggle") is null,"redundant pointer checkbox removed");
                var last=Session().LastAction;Call("SendPointerToGame",(700,40),156);
                Check(Session().Snapshot.KnownGlobalVariables.GetValueOrDefault("var[0x066]")==156&&Session().Verbs.Any(v=>v.VerbId==0x36&&v.Mode==1&&v.ImageObject==159),"normal hover runs native object-156 close-up without an opt-in");
                Check(ReferenceEquals(last,Session().LastAction),"hover sends no click/action");
                var trace=Session().SessionTrace.Count;Call("SendPointerToGame",(701,41),156);Check(Session().SessionTrace.Count==trace,"moving within one object does not add another frame");
                Call("SendPointerToGame",null,null);Check(Session().Verbs.All(v=>v.VerbId!=0x36||v.Mode!=1),"leaving the room clears native hover close-up");
                await Toggle(true);Call("SendPointerToGame",(700,40),156);
                Check(Session().Snapshot.KnownGlobalVariables.GetValueOrDefault("var[0x066]")==156,"Advanced remains interactive with automatic hover");
                await Toggle(false);await Clicks("Advanced round trip");
                await Toggle(true);var interactive=Find<CheckBox>("SessionModeToggle");interactive.IsChecked=false;
                trace=Session().SessionTrace.Count;Call("SendPointerToGame",(700,40),156);Check(Session().SessionTrace.Count==trace,"inspection ignores game hover");
                var saved=Session();trace=Session().SessionTrace.Count;await Toggle(false);
                Check(ReferenceEquals(saved,Session())&&Session().SessionTrace.Count==trace,"return from inspection preserves the same session with no catalog revisit");
                await Clicks("Inspection then normal");
                // Native hover on the sky once more, after retained input control changed.
                Call("SendPointerToGame",(700,40),156);Check(Session().Snapshot.KnownGlobalVariables.GetValueOrDefault("var[0x066]")==156,"normal hover works after inspection toggle");
                var bitmap=new RenderTargetBitmap((int)w.ActualWidth,(int)w.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(w);var enc=new PngBitmapEncoder();enc.Frames.Add(BitmapFrame.Create(bitmap));using(var f=File.Create(Path.Combine(args[1],"normal-hover.png")))enc.Save(f);
                typeof(MainWindow).GetField("_playInstantly",Private)!.SetValue(w,false);await Pick(8);
                var scenes=(Array)typeof(MainWindow).GetField("KnownCutscenes",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
                var trial=scenes.Cast<object>().Single(s=>(int)s.GetType().GetProperty("Script")!.GetValue(s)! == 46);Call("PlayKnownCutscene",trial);await Wait(()=>Session().InCutscene);Call("PauseScenePlayback");
                trace=Session().SessionTrace.Count;Call("SendPointerToGame",(200,70),255);
                Check(Session().SessionTrace.Count==trace&&(bool)Field("_playbackPaused")!&&!Find<ComboBox>("SoundModeCombo").IsEnabled,"hover cannot advance or resume a paused cutscene; sound remains locked");
                report.Add("Real visible WPF layout/hit tests and synthetic handlers; distaff setup is a labelled test seam. No physical input or independent listening.");
            }
            catch(Exception ex){report.Add(ex.ToString());failures++;}
            finally{report.Add($"Failures: {failures}");File.WriteAllLines(Path.Combine(args[1],"input-check.txt"),report);w.Close();app.Shutdown();Environment.ExitCode=failures==0?0:1;}
        };
        app.Run(w);
    }
}

using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Formats;

internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread]
    static void Main(string[] args)
    {
        Directory.CreateDirectory(args[1]);
        var app=new Application(); var window=new MainWindow();
        object? Field(string n)=>typeof(MainWindow).GetField(n,Private)!.GetValue(window);
        object? Call(string n,params object?[] values)=>typeof(MainWindow).GetMethod(n,Private)!.Invoke(window,values);
        LoomExplorationSession Session()=>(LoomExplorationSession)Field("_session")!;
        var report=new List<string>(); int failures=0;
        void Check(bool ok,string text){report.Add($"{(ok?"PASS":"FAIL")} {text}");if(!ok)failures++;}
        window.ContentRendered+=async (_,_)=>
        {
            try
            {
                typeof(MainWindow).GetField("_playInstantly",Private)!.SetValue(window,true);
                Call("OpenGameFolder",args[0]);
                var tree=(TreeView)window.FindName("ResourceTree");
                var item=tree.Items.OfType<TreeViewItem>().SelectMany(g=>g.Items.OfType<TreeViewItem>()).Single(i=>i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString()=="Room" && (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag)==2);
                Call("SelectResourceInTree",item.Tag); await Task.Delay(1000);
                var advanced=(CheckBox)window.FindName("AdvancedModeToggle");
                var interactive=(CheckBox)window.FindName("SessionModeToggle");
                async Task Toggle(bool value){advanced.IsChecked=value;advanced.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));await Task.Delay(300);window.UpdateLayout();}
                async Task Clicks(string label)
                {
                    Check(interactive.IsChecked==true,label+": interactive state enabled");
                    var room=(FrameworkElement)window.FindName("RoomPreview");
                    var viewport=(FrameworkElement)window.FindName("RoomViewport");
                    var hit=window.InputHitTest(viewport.TranslatePoint(new Point(viewport.ActualWidth/2,viewport.ActualHeight/2),window));
                    Check(ReferenceEquals(hit,room),label+$": room hit test reaches RoomPreview ({hit?.GetType().Name})");
                    int before=Session().SessionTrace.Count;
                    Call("HandleRoomPoint",700,40,false);
                    await Task.Delay(100);
                    Check(Session().SessionTrace.Skip(before).Any(s=>s.Contains("runs script 5")),label+": room/object click reaches input script");
                    var setup=new LoomScriptDisassembly(250,1,0,32,16,[new(0,0x0A,"startScript","0x12, [0x0002]","0A 12 02 00 FF"),new(5,0,"stopObjectCode","","00")],null,null,[],[],"Assumed distaff setup");
                    typeof(LoomExplorationSession).GetMethod("RunTopLevelScript",Private)!.Invoke(Session(),[setup]);
                    Call("RefreshRoomPreview");window.UpdateLayout();
                    var image=(Image)window.FindName("VerbStripImage");
                    before=Session().SessionTrace.Count;
                    bool sent=(bool)Call("ClickVerbStrip",new Point(73.5*image.ActualWidth/320,1.5*image.ActualHeight/56))!;
                    Check(sent && Session().SessionTrace.Count>before,label+": visible note click is sent");
                }
                await Toggle(true);await Toggle(false);await Clicks("Advanced round trip");
                await Toggle(true);interactive.IsChecked=false;await Task.Delay(100);
                var savedSession=Session();var traceCount=Session().SessionTrace.Count;
                await Toggle(false);
                Check(ReferenceEquals(savedSession,Session()) && Session().SessionTrace.Count==traceCount,"returning from inspection preserves the session without a catalog revisit");
                await Clicks("Inspection in Advanced then normal");
                report.Add("Real WPF layout/hit testing and synthetic routed toggle/input handlers; no physical input. No room reload by the harness during the round trips.");
            }
            catch(Exception ex){report.Add(ex.ToString());failures++;}
            finally{report.Add($"Failures: {failures}");File.WriteAllLines(Path.Combine(args[1],"mode-check.txt"),report);window.Close();app.Shutdown();Environment.ExitCode=failures==0?0:1;}
        };
        app.Run(window);
    }
}

using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Formats;

internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread]
    static void Main(string[] args)
    {
        var app = new Application(); var window = new MainWindow();
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, values);
        LoomExplorationSession Session() => (LoomExplorationSession)Field("_session")!;
        window.ContentRendered += async (_, _) =>
        {
            var report = new List<string> { "Real WPF window. Distaff enabled through script 018 [2] test seam; room click and note key are assumed input. No physical input/listening proof." };
            int failures=0;
            void Check(bool ok,string text) { if(!ok) failures++; report.Add($"{(ok ? "PASS" : "FAIL")} {text}"); }
            try
            {
                Call("OpenGameFolder", args[0]);
                ((CheckBox)window.FindName("SessionModeToggle")).IsChecked = true;
                var tree = (TreeView)window.FindName("ResourceTree");
                async Task Pick(int room)
                {
                    var item=tree.Items.OfType<TreeViewItem>().SelectMany(g=>g.Items.OfType<TreeViewItem>()).Single(i=>
                        i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString()=="Room" && (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag)==room);
                    Call("SelectResourceInTree",item.Tag);
                    for(int n=0;n<100 && (int?)Field("_currentRoomResource")?.GetType().GetProperty("ResourceId")?.GetValue(Field("_currentRoomResource"))!=room;n++) await Task.Delay(100);
                    await Task.Delay(1000);
                }
                await Pick(2);
                var player=(SceneSoundPlayer)Field("_sceneSound")!;
                report.Add($"Initially follows session: {player.Follows(Session())}");
                if(args.Length<3) Check(player.Follows(Session()),"sound follows the session before its first input");
                var script=new LoomScriptDisassembly(250,1,0,32,16,
                    [new(0,0x0A,"startScript","0x12, [0x0002]","0A 12 02 00 FF"),new(5,0,"stopObjectCode","","00")],null,null,[],[],"Synthetic distaff enable, as existing tests");
                typeof(LoomExplorationSession).GetMethod("RunTopLevelScript",Private)!.Invoke(Session(),[script]);
                Call("SendClickToGame",700,40,false);
                await Task.Delay(4000);
                int eventStart=Session().SoundEvents.Count, logStart=player.Log.Count;
                Call("SendKeyToGame",(int)'e');
                for (int sample=0;sample<16;sample++)
                {
                    await Task.Delay(250);
                    foreach (var voice in ((System.Collections.IEnumerable)typeof(SceneSoundPlayer).GetField("_voices",Private)!.GetValue(player)!).Cast<object>())
                    {
                        var media=(System.Windows.Media.MediaPlayer?)voice.GetType().GetProperty("Player")!.GetValue(voice);
                        if (media is not null) report.Add($"sample {sample}: sound {voice.GetType().GetProperty("SoundId")!.GetValue(voice)}, HasAudio {media.HasAudio}, position {media.Position.TotalSeconds:0.###}, duration {media.NaturalDuration}");
                    }
                }
                report.Add("Note 'e' after aiming at object 156:");
                foreach(var e in Session().SoundEvents.Skip(eventStart)) report.Add($"  event {e.Event}");
                foreach(var e in player.Log.Skip(logStart)) report.Add($"  player {e}");
                if(args.Length<3) Check(player.Log.Skip(logStart).Any(e=>e is {Action:"opened",SoundId:3} && e.Source.Contains("audio True")),"note 3 opens an actual audio track");
                report.Add("Trace: "+string.Join(" | ",Session().SessionTrace.TakeLast(25)));
                report.Add($"Talk: {Session().Messages.Talk?.Text}; playback: {typeof(MainWindow).GetProperty("PlaybackRunning",Private)!.GetValue(window)}");
                if(args.Length<3)
                {
                    // Let script 020's own timeout complete (the old idle cap stopped before this).
                    for(int n=0;n<150 && (bool)typeof(MainWindow).GetProperty("PlaybackRunning",Private)!.GetValue(window)!;n++) await Task.Delay(100);
                    Check(Session().Snapshot.KnownGlobalVariables.GetValueOrDefault("bit[0xD66]")==0,"draft clock reaches script 021's completion instead of stopping at 32 idle frames");
                    Call("ResetSession_Click",null,new RoutedEventArgs());
                    await Pick(2);
                    typeof(LoomExplorationSession).GetMethod("RunTopLevelScript",Private)!.Invoke(Session(),[script]);
                    var loadDraft=new LoomScriptDisassembly(250,1,0,32,16,
                        [new(0,0x0A,"startScript","0x17, [0x0000]","0A 17 00 00 FF"),new(5,0,"stopObjectCode","","00")],null,null,[],[],"Load the boot's literal draft 0 through script 023");
                    typeof(LoomExplorationSession).GetMethod("RunTopLevelScript",Private)!.Invoke(Session(),[loadDraft]);
                    int[] notes=Enumerable.Range(0x52,4).Select(i=>(int)Session().Snapshot.KnownGlobalVariables.GetValueOrDefault($"var[0x{i:X3}]" )).ToArray();
                    report.Add("Literal draft 0 notes: "+string.Join(",",notes));
                    Call("SendClickToGame",700,40,false); await Task.Delay(4000);
                    eventStart=Session().SoundEvents.Count; logStart=player.Log.Count;
                    foreach(var note in notes) { Call("SendKeyToGame",(int)"qwertyui"[note-1]); await Task.Delay(150); }
                    for(int n=0;n<350 && (bool)typeof(MainWindow).GetProperty("PlaybackRunning",Private)!.GetValue(window)!;n++) await Task.Delay(100);
                    foreach(var e in Session().SoundEvents.Skip(eventStart)) report.Add($"  cast event {e.Event}");
                    foreach(var e in player.Log.Skip(logStart)) report.Add($"  cast player {e}");
                    report.Add("Cast trace: "+string.Join(" | ",Session().SessionTrace.TakeLast(35)));
                    Check(player.Log.Skip(logStart).Count(e=>e.Action=="opened" && e.SoundId is >=1 and <=8)>=4,"four-note casting sequence opens all note tracks");
                    Check(player.Log.Skip(logStart).Any(e=>e is {Action:"opened",SoundId:19} && e.Source.StartsWith("audio True")),"the actual spell result's sound 19 opens after the four notes");
                    Check(player.Log.Skip(logStart).Any(e=>e is {Action:"preview start",SoundId:19} && e.Source.StartsWith("Estimated preview route:")),"spell result audio retains its estimated-route label");
                    Check(Session().Snapshot.KnownGlobalVariables.GetValueOrDefault("bit[0xD66]")==0,"four-note draft completes");
                }
                Call("StopScenePlayback");
                await Pick(13); for (int n = 0; n < 200 && Field("_playback") is not null; n++) await Task.Delay(100);
                eventStart=Session().SoundEvents.Count; logStart=player.Log.Count;
                // Room 13 object 325's look plays a draft through scripts 030/031/040; seeded notes are estimates.
                Session().VisitRoomById(13);
                Session().ActivateObjectVerb(325,0x36);
                for(int n=0;n<55;n++) { Call("AdvanceOneFrame"); await Task.Delay(100); }
                report.Add("Direct look at object 325, Advanced Continue path, estimated draft:");
                foreach(var e in Session().SoundEvents.Skip(eventStart)) report.Add($"  event {e.Event}");
                foreach(var e in player.Log.Skip(logStart)) report.Add($"  player {e}");
                if(args.Length<3) Check(!player.Log.Skip(logStart).Any(e=>e.Action=="start") && player.Log.Skip(logStart).Count(e=>e.Action=="preview start")>=4,"Advanced Continue plays the four executed estimated notes with preview labels, never as reached audio");
            }
            catch(Exception ex) { report.Add(ex.ToString()); failures++; }
            finally { report.Add($"Failures: {failures}"); Directory.CreateDirectory(args[1]); File.WriteAllLines(Path.Combine(args[1],"spell-audio-check.txt"),report); window.Close(); app.Shutdown(); Environment.ExitCode=failures==0?0:1; }
        };
        app.Run(window);
    }
}

using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

// WindowCheck <fixture> <private output> <actions.json>. Real MainWindow and its
// frame callback, deterministic after checking its timer. Explicit source visits /
// direct verbs are logged as exploration, never an earned route or a timing check.
internal static class Program
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    [STAThread]
    static void Main(string[] args)
    {
        var output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("SCUMM_EXPLORER_PROFILE_ROOT",Path.Combine(output,"profile"));
        Environment.SetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR",Path.Combine(output,"cache"));
        Environment.SetEnvironmentVariable("SCUMM_HARNESS_FAILURE_LOG",Path.Combine(output,"harness-failure.log"));
        var steps=JsonSerializer.Deserialize<Step[]>(File.ReadAllText(args[2]),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
        var app=new App();app.InitializeComponent();var window=new MainWindow();
        object? Field(string n)=>typeof(MainWindow).GetField(n,Private)!.GetValue(window);
        object? Call(string n,params object?[] p)=>typeof(MainWindow).GetMethod(n,Private)!.Invoke(window,p);
        void Require(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
        void Capture(string name)
        {
            var image=(Image)window.FindName("PlayScreenImage");
            var displayed=new FormatConvertedBitmap((BitmapSource)image.Source,PixelFormats.Bgra32,null,0);
            var pixels=new uint[displayed.PixelWidth*displayed.PixelHeight];displayed.CopyPixels(pixels,displayed.PixelWidth*4,0);
            Require(pixels.SequenceEqual(((ScummPlayFrame)Field("_playShown")!).Pixels),"Actual bitmap differs from composed Play frame.");
            window.UpdateLayout();
            var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);
            bitmap.Render(window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file=File.Create(Path.Combine(output,name+".png"));encoder.Save(file);
        }
        window.ContentRendered+=async(_,_)=>
        {
            var log=new List<string>();ScummGameSession? session=null;
            try
            {
                Call("OpenGameFolder",args[0]);var game=(GameLibrary)Field("_game")!;
                Call("SelectResourceInTree",game.Rooms.First(r=>r.ResourceId==1));
                for(var i=0;i<1200&&Field("_playTimer") is null;i++)await Task.Delay(100);
                session=(ScummGameSession?)Field("_play")??throw new Exception("Play did not start.");
                var timer=(DispatcherTimer?)Field("_playTimer")??throw new Exception("No Play timer.");
                await Task.Delay(200);Require((int)Field("_playFrame")!>0,"Real Play timer did not advance.");timer.Stop();
                async Task Frames(int count)
                {
                    for(var i=0;i<count;i++)
                    {
                        var before=(int)Field("_playFrame")!;
                        for(var wait=0;wait<2000&&(int)Field("_playFrame")! == before;wait++)
                        {Call("PlayTimer_Tick",null,EventArgs.Empty);if((int)Field("_playFrame")! == before)await Task.Delay(10);}
                        Require((int)Field("_playFrame")!>before,"Play callback did not advance (sound preparation?).");
                        if(i%10==0)await Task.Delay(1);
                    }
                }
                async Task Until(Func<bool> condition,int count)
                {for(var i=0;i<count&&!condition();i++)await Frames(1);Require(condition(),"Native condition exceeded frame bound.");}
                async Task Click(int x,int y,int? expected=null)
                {
                    var input=session.ClickScreen(x,y,out var refusal);Require(input is not null,refusal??"Click returned no input.");
                    if(expected is int id)Require(input!.Area==3&&input.Value==id,"Displayed inventory click selected a different object.");
                    log.Add(input!.Description);await Frames(1);
                }
                async Task Inventory(int id)
                {
                    var ego=session.Snapshot.KnownGlobalVariables[ScummVars.V2.Ego];
                    var items=session.CaptureInventoryStatus().Items.Where(i=>i.OwnerId==ego).OrderBy(i=>i.Slot).ToArray();
                    var index=Array.FindIndex(items,i=>i.ObjectId==id);Require(index>=0,"Object is not held by current ego: "+id);
                    // Native inventory keys reset to first page, then scroll down by two.
                    for(var i=0;i<items.Length/2+1;i++){session.PressKey('u');await Frames(1);}
                    while(index>=4){session.PressKey('j');await Frames(1);index-=2;}
                    await Click(index%2==0?8:184,session.MainScreen.Bottom+36+index/2*8,id);
                }
                foreach(var step in steps)
                {
                    var count=step.Count>0?step.Count:2500;
                    switch(step.Op)
                    {
                        case "nativeflight":
                            int Remaining()
                            {
                                var pending=session.PendingScriptSummary.Single(p=>p.StartsWith("Room 11 / Object 241 /",StringComparison.Ordinal));
                                return int.Parse(Regex.Match(pending,@"\(delay (\d+)\)").Groups[1].Value);
                            }
                            var remaining=Remaining(); var delta=Math.Min(session.FrameJiffies,15);
                            Require(remaining>0&&remaining<=28800,"Native cruising timer missing or altered.");
                            await Frames(remaining/delta);
                            Require(session.CurrentRoomId==11&&Remaining()==remaining%delta,"Arrival preceded native timer expiry.");
                            Call("ShowPlayFrame"); Capture("06-last-unexpired-flight-frame");
                            log.Add($"Native flight timer: initial remainder {remaining} jiffies, {remaining/delta} ordinary callbacks leave {Remaining()} jiffies; still cabin11.");
                            await Frames(1);
                            await Until(()=>session.CurrentRoomId==8&&session.PlayerHasControl&&!session.InCutscene,2000);
                            Require(session.Snapshot.KnownGlobalVariables.GetValueOrDefault("var[0x004]")==145,"Wrong native destination airport.");
                            Require(session.Snapshot.ObjectOwners.GetValueOrDefault(82)==14&&session.Snapshot.KnownGlobalVariables.GetValueOrDefault("var[0x09B]")==0,"Consumed ticket/departure state lost.");
                            Require(session.Snapshot.ObjectOwners.GetValueOrDefault(51)==1,"Native peanuts lost during arrival.");
                            Require(!session.PendingScriptSummary.Any(p=>p.StartsWith("Room 11 / Object 241 /",StringComparison.Ordinal)||p.StartsWith("Script 79 ",StringComparison.Ordinal)),"Completed flight scripts remain pending.");
                            Require(!session.Snapshot.Uncertainties.Any(u=>u.Contains("unsupported v2",StringComparison.Ordinal)),"Unsupported opcode in arrival.");
                            log.Add("Native automatic arrival: physical Room8/logical145, control returned, consumed ticket retained, peanuts51 owner1; no extra input or skipped delay.");
                            break;
                        case "frames":await Frames(step.Count);break;
                        case "control":await Until(()=>session.PlayerHasControl&&!session.InCutscene,count);break;
                        case "untilroom":await Until(()=>session.CurrentRoomId==step.Value,count);break;
                        case "untilskip":await Until(()=>session.CutsceneSkippable,count);break;
                        case "untilview":await Until(()=>session.ViewLeft>=step.Value,count);break;
                        case "untilowner":await Until(()=>session.Snapshot.ObjectOwners.GetValueOrDefault(step.Object)==step.Value,count);break;
                        case "untilvariable":await Until(()=>session.Snapshot.KnownGlobalVariables.GetValueOrDefault(step.Text!)==step.Value,count);break;
                        case "untilmessage":await Until(()=>session.Messages.OnScreen.Any(m=>m.Text.Contains(step.Text!,StringComparison.Ordinal)),count);break;
                        case "visit":session.VisitRoom(game.Rooms.Single(r=>r.ResourceId==step.Value));log.Add("Exploration room preview "+step.Value);break;
                        case "settle":session.Settle();log.Add("Exploration bounded native settle.");break;
                        case "activate":session.ActivateObjectVerb(step.Object,step.Value);log.Add($"Exploration direct verb {step.Value} object {step.Object}");break;
                        case "click":await Click(step.X,step.Y);break;
                        case "roomclick":Require(step.X-session.ViewLeft is >=0 and <320,"Room target outside view.");await Click(step.X-session.ViewLeft,step.Y+session.MainScreen.Top);break;
                        case "verb":var v=session.PlayVerbArea().Items.Single(v=>v.VerbId==step.Value&&v.Mode==1);await Click(v.X+Math.Min(4,v.Width/2),v.Y+3);break;
                        case "inventory":await Inventory(step.Object);break;
                        case "execute":await Click(8,session.MainScreen.Bottom+4);break;
                        case "key":session.PressKey(step.Value);await Frames(1);break;
                        case "owner":Require(session.Snapshot.ObjectOwners.GetValueOrDefault(step.Object)==step.Value,"Unexpected owner of "+step.Object);break;
                        case "variable":Require(session.Snapshot.KnownGlobalVariables.GetValueOrDefault(step.Text!)==step.Value,"Unexpected variable "+step.Text);break;
                        case "room":Require(session.CurrentRoomId==step.Value,"Unexpected current room.");break;
                        case "view":Require(session.ViewLeft==step.Value,"Unexpected camera left.");break;
                        case "sentence":Require(ScummTextDecoder.DecodeGlyphs(session.PlaySentenceGlyphs()).Contains(step.Text!,StringComparison.Ordinal),"Sentence differs.");break;
                        case "capture":var state=JsonSerializer.Serialize(session.Snapshot);var frame=(int)Field("_playFrame")!;Call("ShowPlayFrame");Capture(step.Text!);Require(state==JsonSerializer.Serialize(session.Snapshot)&&(int)Field("_playFrame")! == frame,"Capture changed game state/frame.");break;
                        default:throw new Exception("Unknown step "+step.Op);
                    }
                    log.Add($"{step.Op}: room={session.CurrentRoomId}, frame={Field("_playFrame")}, owner51={session.Snapshot.ObjectOwners.GetValueOrDefault(51)}, inputs={session.DeliveredInput.Count}, sentence={ScummTextDecoder.DecodeGlyphs(session.PlaySentenceGlyphs())}");
                    File.WriteAllLines(Path.Combine(output,"progress.txt"),log);
                }
                var image=(Image)window.FindName("PlayScreenImage");Require(image.IsVisible&&image.ToolTip is null,"Play absent or technical tooltip in Normal.");
                Require(((TextBlock)window.FindName("ObjectsNote")).Text=="","Technical note in Normal.");
                File.WriteAllLines(Path.Combine(output,"result.txt"),log.Append("PASS actual-window configured Zak actions."));Call("StopPlay");app.Shutdown(0);
            }
            catch(Exception ex){File.WriteAllText(Path.Combine(output,"failure.txt"),ex+"\n"+string.Join("\n",log));app.Shutdown(1);}
        };
        app.Run(window);
    }
    private sealed record Step(string Op,int X=0,int Y=0,int Count=0,int Object=0,int Value=0,string? Text=null);
}

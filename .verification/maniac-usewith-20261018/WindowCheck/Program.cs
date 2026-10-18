using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

// WindowCheck <fixture> <output> <actions.json>. Private real MainWindow; native inputs
// and its actual frame/render callback. Preparation room visits/direct verbs are
// explicitly recorded exploration actions, never an earned story route or timing test.
internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread]
    static void Main(string[] args)
    {
        var output = Path.GetFullPath(args[1]); Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("SCUMM_EXPLORER_PROFILE_ROOT",Path.Combine(output,"profile"));
        Environment.SetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR",Path.Combine(output,"cache"));
        Environment.SetEnvironmentVariable("SCUMM_HARNESS_FAILURE_LOG",Path.Combine(output,"harness-failure.log"));
        var actions=JsonSerializer.Deserialize<Step[]>(File.ReadAllText(args[2]),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
        var app=new App(); app.InitializeComponent(); var window=new MainWindow();
        object? Field(string n)=>typeof(MainWindow).GetField(n,Private)!.GetValue(window);
        object? Call(string n,params object?[] p)=>typeof(MainWindow).GetMethod(n,Private)!.Invoke(window,p);
        void Require(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
        void Capture(string name)
        {
            window.UpdateLayout();
            var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);
            bitmap.Render(window); var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream=File.Create(Path.Combine(output,name+".png")); encoder.Save(stream);
        }
        uint[] DisplayPixels()
        {
            var image=(Image)window.FindName("PlayScreenImage");
            var bitmap=new FormatConvertedBitmap((BitmapSource)image.Source,PixelFormats.Bgra32,null,0);
            var pixels=new uint[bitmap.PixelWidth*bitmap.PixelHeight];bitmap.CopyPixels(pixels,bitmap.PixelWidth*4,0);
            Require(pixels.SequenceEqual(((ScummPlayFrame)Field("_playShown")!).Pixels),"Actual Play bitmap differs from composed frame.");
            return pixels;
        }
        window.ContentRendered+=async(_,_)=>
        {
            var log=new List<string>();
            try
            {
                Call("OpenGameFolder",args[0]); var game=(GameLibrary)Field("_game")!;
                Call("SelectResourceInTree",game.Rooms.First(r=>r.ResourceId==44));
                for(var i=0;i<1200&&Field("_playTimer") is null;i++)await Task.Delay(100);
                var session=(ScummGameSession?)Field("_play")??throw new Exception("Play did not start.");
                var timer=(DispatcherTimer?)Field("_playTimer")??throw new Exception("No real Play timer.");
                await Task.Delay(200); Require((int)Field("_playFrame")!>0,"Play timer did not advance."); timer.Stop();
                async Task Frames(int count)
                {
                    for(var i=0;i<count;i++)
                    {
                        var before=(int)Field("_playFrame")!;
                        for(var wait=0;wait<2000&&(int)Field("_playFrame")! == before;wait++)
                        { Call("PlayTimer_Tick",null,EventArgs.Empty); if((int)Field("_playFrame")! == before)await Task.Delay(10); }
                        Require((int)Field("_playFrame")!>before,"Play clock did not advance (sound preparation?).");
                        if(i%10==0)await Task.Delay(1);
                    }
                }
                async Task Until(Func<bool> condition,int limit=2500)
                {for(var i=0;i<limit&&!condition();i++)await Frames(1);Require(condition(),"Native condition exceeded its frame bound.");}
                async Task Click(int x,int y)
                {var input=session.ClickScreen(x,y,out var refused);Require(input is not null,refused??"Click returned no input.");log.Add(input!.Description);await Frames(1);}
                async Task Inventory(int id)
                {
                    var ego=session.Snapshot.KnownGlobalVariables[ScummVars.V2.Ego];
                    var items=session.CaptureInventoryStatus().Items.Where(i=>i.OwnerId==ego).OrderBy(i=>i.Slot).ToArray();
                    var slot=Array.FindIndex(items,i=>i.ObjectId==id);Require(slot is >=0 and <4,"Target not in displayed inventory slots: "+id);
                    await Click(slot%2==0?8:184,session.MainScreen.Bottom+36+slot/2*8);
                }
                foreach(var step in actions)
                {
                    switch(step.Op)
                    {
                        case "click":await Click(step.X,step.Y);break;
                        case "menu":await Click(step.X,step.Y+session.MainScreen.Top);break;
                        case "frames":await Frames(step.Frames);break;
                        case "control":await Until(()=>session.PlayerHasControl&&!session.InCutscene);break;
                        case "openingSkip":await Until(()=>session.CurrentRoomId==44&&session.CutsceneSkippable);session.PressKey(27);await Until(()=>session.PlayerHasControl);break;
                        case "visit":session.VisitRoom(game.Rooms.Single(r=>r.ResourceId==step.Room));log.Add("Exploration room preview "+step.Room);break;
                        case "settle":session.Settle();log.Add("Exploration preparation: native bounded settle.");break;
                        case "activate":session.ActivateObjectVerb(step.Object,step.Verb);log.Add($"Exploration direct verb {step.Verb} object {step.Object}");break;
                        case "verb":var v=session.PlayVerbArea().Items.Single(v=>v.VerbId==step.Verb&&v.Mode==1);await Click(v.X+Math.Min(4,v.Width/2),v.Y+3);break;
                        case "inventory":await Inventory(step.Object);break;
                        case "sentence":await Click(8,session.MainScreen.Bottom+4);break;
                        case "text":await Until(()=>ScummTextDecoder.DecodeGlyphs(session.PlaySentenceGlyphs()).Contains(step.Text!,StringComparison.Ordinal));break;
                        case "battery":await Until(()=>session.Snapshot.KnownGlobalVariables.GetValueOrDefault("var[0x032]")==2&&session.Snapshot.ObjectOwners.GetValueOrDefault(167)==0);break;
                        case "owner":Require(session.Snapshot.ObjectOwners.GetValueOrDefault(step.Object)==step.Value,"Unexpected owner for "+step.Object);break;
                        case "capture":Call("ShowPlayFrame");Capture(step.Text!);break;
                        case "beam":
                            var beforeFrame=(int)Field("_playFrame")!;session.PointAtScreen((179,79));var snapshot=JsonSerializer.Serialize(session.Snapshot);Call("ShowPlayFrame");
                            var screen=(ScummPlayScreen)Field("_playScreen")!;var first=screen.Scene()!.Compose();
                            var firstDisplayed=DisplayPixels();
                            Require((session.CaptureRoom().Lights&4)!=0&&(session.CaptureRoom().Lights&2)==0,"Native source did not enable the beam.");
                            Require(first.Any(p=>p!=0)&&first.Any(p=>p==0),"Missing bounded flashlight window.");Capture("beam-first");
                            Require(snapshot==JsonSerializer.Serialize(session.Snapshot),"Rendering changed game state.");
                            session.PointAtScreen((250,100));Call("ShowPlayFrame");var second=screen.Scene()!.Compose();
                            Require(!first.SequenceEqual(second),"Pointer did not move native beam.");Capture("beam-moved");
                            Require(!firstDisplayed.SequenceEqual(DisplayPixels()),"Pointer did not move the actual Play bitmap beam.");
                            Require((int)Field("_playFrame")! == beforeFrame,"Rendering advanced a game frame.");
                            log.Add("PASS native moving beam; rendering uses reached lighting and no story override.");break;
                        default:throw new Exception("Unknown step "+step.Op);
                    }
                    log.Add($"{step.Op}: room {session.CurrentRoomId}, frame {Field("_playFrame")}, sentence {ScummTextDecoder.DecodeGlyphs(session.PlaySentenceGlyphs())}, battery {session.Snapshot.KnownGlobalVariables.GetValueOrDefault("var[0x032]")}");
                }
                var image=(Image)window.FindName("PlayScreenImage");Require(image.IsVisible&&image.ToolTip is null,"Play image absent or technical tooltip in Normal.");
                Require(((TextBlock)window.FindName("ObjectsNote")).Text=="","Technical note in Normal.");
                File.WriteAllLines(Path.Combine(output,"result.txt"),log.Append("PASS actual-window Use-with route."));Call("StopPlay");app.Shutdown(0);
            }
            catch(Exception ex){File.WriteAllText(Path.Combine(output,"failure.txt"),ex+"\n"+string.Join("\n",log));app.Shutdown(1);}
        };
        app.Run(window);
    }
    private sealed record Step(string Op,int X=0,int Y=0,int Frames=0,int Room=0,int Object=0,int Verb=0,int Value=0,string? Text=null);
}

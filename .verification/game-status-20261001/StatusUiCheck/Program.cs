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
        object? StatusCall(GameStatusWindow s,string n)=>typeof(GameStatusWindow).GetMethod(n,Private)!.Invoke(s,[s,new RoutedEventArgs()]);
        LoomExplorationSession Session()=>(LoomExplorationSession)Field("_session")!;
        void Check(bool ok,string text){report.Add($"{(ok?"PASS":"FAIL")} {text}");if(!ok)failures++;}
        void Capture(Window window,string name){window.UpdateLayout();var b=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);b.Render(window);var enc=new PngBitmapEncoder();enc.Frames.Add(BitmapFrame.Create(b));using var f=File.Create(Path.Combine(args[1],name+".png"));enc.Save(f);}
        w.ContentRendered+=async (_,_)=>
        {
            async Task Wait(Func<bool> c,int limit=3000){for(int i=0;i<limit&&!c();i++)await Task.Delay(50);if(!c())throw new TimeoutException();}
            async Task Pick(int id){Call("SelectResourceInTree",((GameLibrary)Field("_game")!).Rooms.Single(r=>r.ResourceId==id));await Wait(()=>((GameResource?)Field("_currentRoomResource"))?.ResourceId==id&&((Image)w.FindName("RoomPreview")).IsVisible);await Task.Delay(250);}
            GameStatusWindow? status=null;
            try
            {
                Call("OpenGameFolder",args[0]);await Pick(2);await Wait(()=>Session().GameStatus.CanEdit);
                Check(((Button)w.FindName("GameStatusButton")).IsVisible,"normal room exposes Game status without Advanced");
                status=(GameStatusWindow)Call("BuildGameStatusWindow")!;status.Show();await Task.Delay(300);
                var staff=(ComboBox)status.FindName("StaffChoice");var count=(ComboBox)status.FindName("NoteCountChoice");var grid=(DataGrid)status.FindName("DraftGrid");
                Check(grid.Items.Count==15&&Session().GameStatus.Supported,"loaded scripts provide fifteen castable drafts");
                Check(!staff.IsEnabled&&grid.IsReadOnly,"inspection has no implicit edits");Capture(status,"status-inspection");
                var original=Session().GameStatus.Drafts[0];StatusCall(status,"Edit_Click");
                staff.SelectedIndex=1;count.SelectedIndex=8;var row=(GameStatusWindow.DraftRow)grid.Items[0];row.Note1=1;row.Note2=2;row.Note3=1;row.Note4=2;
                StatusCall(status,"Apply_Click");
                var edited=Session().GameStatus;
                Check(edited.StaffHeld==true&&edited.AvailableNoteCount==8&&edited.Drafts[0].Notes.SequenceEqual(new[]{1,2,1,2}),"Apply updates this same session's staff, note count and four-note sequence");
                Check((original.Packed!.Value&0xF000)==(edited.Drafts[0].Packed!.Value&0xF000)&&original.ForwardVerb==edited.Drafts[0].ForwardVerb,"sequence edits preserve learned/reverse flags and response target");
                Check(edited.Drafts[0].Origin.Contains("Explicit user")&&edited.StaffOrigin.Contains("Explicit user"),"status distinguishes explicit user choices from source prerequisites");
                Check(((TextBlock)status.FindName("NoteIdentities")).Text.EndsWith("Note 8"),"all eight available identities are visible outside the compact count selector");
                var displayed=grid.ItemsSource;await Task.Delay(350);Check(ReferenceEquals(displayed,grid.ItemsSource),"unchanged live refresh preserves the browsed draft rows");
                Check(Session().Snapshot.KnownGlobalVariables["var[0x064]"]==2&&Session().VerbArea().Items.Count(i=>i.VerbId is >=1 and <=8)==8,"source scripts refresh the native note bar");Capture(status,"status-edited");status.Close();status=null;Capture(w,"room-with-edited-staff");
                Call("HandleRoomPoint",700,40,false);await Wait(()=>Field("_playback") is null);
                var same=Session();
                Call("SendInputToGame",(Func<LoomObjectActionResult>)(()=> { var note=Session().VerbArea().Items.Single(i=>i.VerbId==1);return Session().ClickVerbArea(note.X+1,note.Y+1); }),"Status test note input",null);
                Check(ReferenceEquals(same,Session())&&Session().Snapshot.KnownGlobalVariables["var[0x056]"]==1,"subsequent native note input records the edited session's available note");
                await Wait(()=>Field("_playback") is null);
                var scene=((IEnumerable)Call("AllCutscenes")!).Cast<object>().First(s=>(int)s.GetType().GetProperty("RoomId")!.GetValue(s)! == 8);
                Call("PlayKnownCutscene",scene);await Wait(()=>Session().InCutscene);Call("PauseScenePlayback");
                status=(GameStatusWindow)Call("BuildGameStatusWindow")!;status.Show();await Task.Delay(300);
                Check(!((Button)status.FindName("EditButton")).IsEnabled,"paused replay permits inspection and locks edits");
                var before=Session().GameStatus;StatusCall(status,"Edit_Click");StatusCall(status,"Apply_Click");
                Check(Session().GameStatus.StaffHeld==before.StaffHeld&&Session().GameStatus.Drafts[0].Packed==before.Drafts[0].Packed,"guarded handlers cannot rewrite paused prerequisites");Capture(status,"status-paused");status.Close();status=null;
                await Pick(2);await Wait(()=>Session().GameStatus.CanEdit);
                Check(!Session().GameStatus.Drafts[0].Origin.Contains("Explicit user"),"fresh replay replaces exploration edits and their provenance");
                status=(GameStatusWindow)Call("BuildGameStatusWindow")!;status.Show();await Task.Delay(300);StatusCall(status,"Edit_Click");
                ((ComboBox)status.FindName("StaffChoice")).SelectedIndex=1;StatusCall(status,"Cancel_Click");
                Check(Session().GameStatus.StaffHeld==false,"Cancel discards pending changes");
                Session().UpdateStaffPossession(true);await Task.Delay(300);
                Check(((ComboBox)status.FindName("StaffChoice")).SelectedIndex==1,"open status refreshes live session changes");
                StatusCall(status,"Edit_Click");((ComboBox)status.FindName("StaffChoice")).SelectedIndex=0;
                Session().UpdateAvailableNotes(3);StatusCall(status,"Apply_Click");
                Check(Session().GameStatus.StaffHeld==true&&Session().GameStatus.AvailableNoteCount==3&&((TextBlock)status.FindName("EditMessage")).Text.Contains("changed while editing"),"source/session change while editing prevents stale Apply");StatusCall(status,"Cancel_Click");
                status.Width=820;status.Height=520;await Task.Delay(250);Capture(status,"status-minimum-size");status.Close();status=null;
            }
            catch(Exception ex){report.Add(ex.ToString());failures++;}
            finally{status?.Close();report.Add("Visible WPF modal-equivalent owned window, synthetic controls/native note input. No physical-input/listening or original-game prerequisite claim.");report.Add($"Failures: {failures}");File.WriteAllLines(Path.Combine(args[1],"status-ui-check.txt"),report);w.Close();app.Shutdown();Environment.ExitCode=failures==0?0:1;}
        };app.Run(w);
    }
}

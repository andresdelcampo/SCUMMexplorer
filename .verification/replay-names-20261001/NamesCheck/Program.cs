using System.Collections;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Domain;

internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread] static void Main(string[] args)
    {
        Directory.CreateDirectory(args[1]); var app=new Application(); var w=new MainWindow(); var report=new List<string>(); int failures=0;
        object? Field(string n)=>typeof(MainWindow).GetField(n,Private)!.GetValue(w);
        object? Call(string n,params object?[] v)=>typeof(MainWindow).GetMethod(n,Private)!.Invoke(w,v);
        T Prop<T>(object o,string n)=>(T)o.GetType().GetProperty(n)!.GetValue(o)!;
        void Check(bool ok,string text){report.Add($"{(ok?"PASS":"FAIL")} {text}");if(!ok)failures++;}
        void Capture(Window window,string name){window.UpdateLayout();var b=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);b.Render(window);var enc=new PngBitmapEncoder();enc.Frames.Add(BitmapFrame.Create(b));using var f=File.Create(Path.Combine(args[1],name+".png"));enc.Save(f);}
        w.ContentRendered+=async (_,_)=>
        {
            try
            {
                Call("OpenGameFolder",args[0]);
                for(int i=0;i<200 && ((GameResource?)Field("_currentRoomResource"))?.ResourceId!=2;i++)await Task.Delay(50);
                var all=((IEnumerable)Call("AllCutscenes")!).Cast<object>().ToArray();
                Check(all.Length==290,"catalog retains all 290 object/entry starts plus opening");
                Check(all.All(s=>!System.Text.RegularExpressions.Regex.IsMatch(Prop<string>(s,"Title"),@"Object \d+")),"no numeric object placeholders in playable titles");
                Check(all.Any(s=>Prop<string>(s,"Title")=="Room 02 · Draft response 1 at cielo"),"stored Spanish sky name preserved");
                Check(all.Where(s=>Prop<string>(s,"Source").Contains("Naming gap:")).Count()==39,"39 unnamed targets retain explicit naming gaps in source details");
                var gallery=(IDictionary)Call("GalleryCutscenes")!;
                Check(gallery.Keys.Cast<int>().Where(id=>id!=44).All(id=>all.Any(s=>Prop<string>(s,"Title")==Prop<string>(gallery[id]!,"Title"))),"all Game Texts replay titles use shared descriptors");
                var picker=(Window)Call("BuildCutscenePicker")!;
                picker.ContentRendered+=(_,_)=>
                {
                    try
                    {
                        ((ComboBox)picker.FindName("CategoryFilter")).SelectedIndex=2;
                        var list=(ListBox)picker.FindName("SceneList");
                        Check(list.Items.Count==291,"global picker retains all 291 starts");
                        Check(list.Items.Cast<object>().Where(s=>Prop<string>(s,"Title")!="The opening").All(s=>all.Any(c=>Prop<string>(c,"Title")==Prop<string>(s,"Title"))),"global picker shares every replay title");
                        ((TextBox)picker.FindName("SearchBox")).Text="Room 02 · Use response";
                        Check(list.Items.Count==1 && ((TextBox)picker.FindName("SourceDetails")).Text.Contains("Naming gap:"),"unnamed transition searchable by action, naming gap shown on demand");
                        Capture(picker,"unnamed-transition");
                        ((TextBox)picker.FindName("SearchBox")).Text="cielo"; Capture(picker,"source-name");
                    }
                    catch(Exception ex){report.Add(ex.ToString());failures++;}
                    finally{picker.Close();}
                };
                picker.ShowDialog();
                Check(!((Button)w.FindName("GameTextsButton")).IsVisible,"Game Texts remains Advanced-only");
            }
            catch(Exception ex){report.Add(ex.ToString());failures++;}
            finally{report.Add($"Failures: {failures}");File.WriteAllLines(Path.Combine(args[1],"names-check.txt"),report);w.Close();app.Shutdown();Environment.ExitCode=failures==0?0:1;}
        };
        app.Run(w);
    }
}

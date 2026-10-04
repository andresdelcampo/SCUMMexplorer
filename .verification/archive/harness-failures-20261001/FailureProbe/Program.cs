using System.Windows;
using System.Windows.Threading;

internal static class Program
{
    [STAThread] static void Main(string[] args)
    {
        if (args[0] == "main") throw new InvalidOperationException("Deliberate main-thread crash");
        if (args[0] == "worker")
        {
            new Thread(() => throw new InvalidOperationException("Deliberate worker-thread crash")).Start();
            Thread.Sleep(Timeout.Infinite); return;
        }
        var app = new Application();
        Dispatcher.CurrentDispatcher.BeginInvoke(() => throw new InvalidOperationException("Deliberate WPF dispatcher crash"));
        app.Run();
    }
}

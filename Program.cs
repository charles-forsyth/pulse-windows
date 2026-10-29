using Microsoft.UI.Dispatching;
using Pulse.Services;

namespace Pulse;

public static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        Trace("Main start");
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Trace("ComWrappers ok");
        Application.Start(p =>
        {
            Trace("Application.Start callback");
            var ctx = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(ctx);
            new App();
            Trace("App constructed");
        });
        return 0;
    }

    internal static void Trace(string m)
    {
        if (Environment.GetEnvironmentVariable("PULSE_TRACE") == "1") SettingsService.Log(m);
    }
}

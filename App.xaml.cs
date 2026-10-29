using Pulse.Services;

namespace Pulse;

public partial class App : Application
{
    public static MainWindow? MainWindow { get; private set; }

    public App()
    {
        if (Environment.GetEnvironmentVariable("PULSE_TRACE") == "1")
            AppDomain.CurrentDomain.FirstChanceException += (_, e) =>
                SettingsService.Log($"FirstChance: {e.Exception.GetType().Name}: {e.Exception.Message}{Environment.NewLine}{e.Exception.StackTrace}");
        InitializeComponent();
        UnhandledException += (_, e) =>
        {
            SettingsService.Log("Unhandled UI exception: " + e.Exception);
            e.Handled = true; // keep a dashboard alive on transient errors; details go to the log
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => SettingsService.Log("Fatal: " + e.ExceptionObject);
        TaskScheduler.UnobservedTaskException += (_, e) => { SettingsService.Log("Task: " + e.Exception); e.SetObserved(); };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        SettingsService.Load();
        try
        {
            MainWindow = new MainWindow();
            MainWindow.Activate();
        }
        catch (Exception ex)
        {
            SettingsService.Log("Startup failed: " + ex);
            throw;
        }
    }
}

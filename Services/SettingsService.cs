using System.Text.Json;

namespace Pulse.Services;

public sealed class AppSettings
{
    public string Theme { get; set; } = "System";      // Light | Dark | System
    public string Accent { get; set; } = "System";     // System | #RRGGBB
    public string Backdrop { get; set; } = "Mica";     // Mica | MicaAlt | Acrylic
    public double IntervalSeconds { get; set; } = 1.0;
}

/// <summary>Settings persisted as JSON in %LOCALAPPDATA%\Pulse (the app is unpackaged, so no ApplicationData).</summary>
public static class SettingsService
{
    private static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pulse");
    private static readonly string FilePath = Path.Combine(Dir, "settings.json");

    public static AppSettings Current { get; private set; } = new();

    public static event Action? Changed;

    public static void Load()
    {
        try
        {
            if (File.Exists(FilePath))
                Current = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch { Current = new AppSettings(); }
        if (Current.IntervalSeconds is < 0.25 or > 60) Current.IntervalSeconds = 1;
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* non-fatal */ }
        Changed?.Invoke();
    }

    public static string LogPath => Path.Combine(Dir, "pulse.log");

    public static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch { }
    }
}

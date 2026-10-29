namespace Pulse.Services;

public static class Fmt
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB", "PB"];

    public static string Bytes(double b, int decimals = 1)
    {
        if (b < 0 || double.IsNaN(b)) b = 0;
        int u = 0;
        while (b >= 1024 && u < Units.Length - 1) { b /= 1024; u++; }
        return u == 0 ? $"{b:0} {Units[u]}" : b.ToString("0." + new string('0', decimals)) + " " + Units[u];
    }

    public static string Rate(double bytesPerSec) => Bytes(bytesPerSec) + "/s";

    public static string Bits(double bitsPerSec)
    {
        string[] u = ["bps", "Kbps", "Mbps", "Gbps", "Tbps"];
        int i = 0;
        while (bitsPerSec >= 1000 && i < u.Length - 1) { bitsPerSec /= 1000; i++; }
        return $"{bitsPerSec:0.#} {u[i]}";
    }

    public static string Uptime(TimeSpan t)
    {
        if (t.TotalDays >= 1) return $"{(int)t.TotalDays}d {t.Hours}h {t.Minutes}m";
        if (t.TotalHours >= 1) return $"{t.Hours}h {t.Minutes}m {t.Seconds}s";
        return $"{t.Minutes}m {t.Seconds}s";
    }
}

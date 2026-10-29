using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Pulse.Services;

public sealed record InfoRow(string Label, string Value);

public sealed class DisplayInfo
{
    public string Name { get; init; } = "";
    public string Adapter { get; init; } = "";
    public string Resolution { get; init; } = "";
    public string Refresh { get; init; } = "";
    public string Scale { get; init; } = "";
    public bool Primary { get; init; }
}

public sealed class GpuInfo
{
    public string Name { get; init; } = "";
    public string Driver { get; init; } = "";
    public string Memory { get; init; } = "";
    public string Mode { get; init; } = "";
}

public sealed class SystemInfo
{
    public List<InfoRow> Os { get; } = new();
    public List<InfoRow> Cpu { get; } = new();
    public List<InfoRow> Machine { get; } = new();
    public List<GpuInfo> Gpus { get; } = new();
    public List<DisplayInfo> Displays { get; } = new();
    public string OsTitle { get; set; } = "Windows";
    public string CpuTitle { get; set; } = "";
    public string MachineName { get; set; } = Environment.MachineName;
}

/// <summary>Static system inventory gathered from WMI, the registry and Win32. Cached after first load.</summary>
public static class SystemInfoService
{
    private static Task<SystemInfo>? _task;
    private static string? _cpuName;

    public static Task<SystemInfo> GetAsync() => _task ??= Task.Run(Collect);

    /// <summary>Fast CPU name from the registry (no WMI).</summary>
    public static string CpuName
    {
        get
        {
            if (_cpuName != null) return _cpuName;
            try
            {
                using var k = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                _cpuName = (k?.GetValue("ProcessorNameString") as string)?.Trim();
            }
            catch { }
            return _cpuName ??= "Processor";
        }
    }

    private static SystemInfo Collect()
    {
        var info = new SystemInfo();

        // ---------- OS
        string caption = "Windows", version = Environment.OSVersion.Version.ToString(), build = "", installDate = "", arch = "";
        string displayVersion = "";
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            displayVersion = k?.GetValue("DisplayVersion") as string ?? "";
            var ubr = k?.GetValue("UBR");
            var cb = k?.GetValue("CurrentBuild") as string;
            if (cb != null) build = ubr != null ? $"{cb}.{ubr}" : cb;
        }
        catch { }
        try
        {
            using var s = new ManagementObjectSearcher("SELECT Caption, Version, InstallDate, OSArchitecture, LastBootUpTime, RegisteredUser FROM Win32_OperatingSystem");
            foreach (ManagementObject o in s.Get())
            {
                caption = (o["Caption"] as string)?.Replace("Microsoft ", "").Trim() ?? caption;
                version = o["Version"] as string ?? version;
                arch = o["OSArchitecture"] as string ?? "";
                if (o["InstallDate"] is string id) installDate = ManagementDateTimeConverter.ToDateTime(id).ToString("d MMM yyyy");
                if (o["LastBootUpTime"] is string lb)
                    info.Os.Add(new("Last boot", ManagementDateTimeConverter.ToDateTime(lb).ToString("ddd d MMM yyyy, HH:mm")));
            }
        }
        catch (Exception ex) { SettingsService.Log("WMI OS: " + ex.Message); }

        info.OsTitle = string.IsNullOrEmpty(displayVersion) ? caption : $"{caption} {displayVersion}";
        info.Os.Insert(0, new("Edition", caption));
        if (displayVersion != "") info.Os.Insert(1, new("Version", displayVersion));
        info.Os.Add(new("Build", string.IsNullOrEmpty(build) ? version : build));
        info.Os.Add(new("OS architecture", $"{RuntimeInformation.OSArchitecture} {(arch != "" ? $"({arch})" : "")}".Trim()));
        info.Os.Add(new("App architecture", $"{RuntimeInformation.ProcessArchitecture}{(RuntimeInformation.ProcessArchitecture != RuntimeInformation.OSArchitecture ? " (emulated)" : " (native)")}"));
        if (installDate != "") info.Os.Add(new("Installed", installDate));
        info.Os.Add(new(".NET runtime", RuntimeInformation.FrameworkDescription));

        // ---------- CPU
        info.CpuTitle = CpuName;
        try
        {
            using var s = new ManagementObjectSearcher("SELECT Name, Manufacturer, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed, L2CacheSize, L3CacheSize, SocketDesignation FROM Win32_Processor");
            int sockets = 0, cores = 0, logical = 0; uint maxClock = 0, l2 = 0, l3 = 0; string manu = "";
            foreach (ManagementObject o in s.Get())
            {
                sockets++;
                cores += Convert.ToInt32(o["NumberOfCores"] ?? 0);
                logical += Convert.ToInt32(o["NumberOfLogicalProcessors"] ?? 0);
                maxClock = Math.Max(maxClock, Convert.ToUInt32(o["MaxClockSpeed"] ?? 0u));
                l2 += Convert.ToUInt32(o["L2CacheSize"] ?? 0u);
                l3 += Convert.ToUInt32(o["L3CacheSize"] ?? 0u);
                manu = o["Manufacturer"] as string ?? manu;
                if (o["Name"] is string n && !string.IsNullOrWhiteSpace(n)) info.CpuTitle = n.Trim();
            }
            info.Cpu.Add(new("Model", info.CpuTitle));
            if (manu != "") info.Cpu.Add(new("Manufacturer", manu));
            info.Cpu.Add(new("Sockets", sockets.ToString()));
            info.Cpu.Add(new("Physical cores", cores > 0 ? cores.ToString() : "—"));
            info.Cpu.Add(new("Logical processors", (logical > 0 ? logical : Environment.ProcessorCount).ToString()));
            if (maxClock > 0) info.Cpu.Add(new("Base clock", $"{maxClock / 1000.0:0.00} GHz"));
            if (l2 > 0) info.Cpu.Add(new("L2 cache", Fmt.Bytes(l2 * 1024UL)));
            if (l3 > 0) info.Cpu.Add(new("L3 cache", Fmt.Bytes(l3 * 1024UL)));
        }
        catch (Exception ex)
        {
            SettingsService.Log("WMI CPU: " + ex.Message);
            info.Cpu.Add(new("Model", info.CpuTitle));
            info.Cpu.Add(new("Logical processors", Environment.ProcessorCount.ToString()));
        }

        // ---------- Machine
        try
        {
            using var s = new ManagementObjectSearcher("SELECT Manufacturer, Model, TotalPhysicalMemory FROM Win32_ComputerSystem");
            foreach (ManagementObject o in s.Get())
            {
                info.Machine.Add(new("Device name", Environment.MachineName));
                info.Machine.Add(new("Manufacturer", (o["Manufacturer"] as string ?? "—").Trim()));
                info.Machine.Add(new("Model", (o["Model"] as string ?? "—").Trim()));
                info.Machine.Add(new("Installed RAM", Fmt.Bytes(Convert.ToUInt64(o["TotalPhysicalMemory"] ?? 0UL))));
            }
        }
        catch (Exception ex) { SettingsService.Log("WMI CS: " + ex.Message); info.Machine.Add(new("Device name", Environment.MachineName)); }
        try
        {
            using var s = new ManagementObjectSearcher("SELECT SMBIOSBIOSVersion, Manufacturer FROM Win32_BIOS");
            foreach (ManagementObject o in s.Get())
                info.Machine.Add(new("Firmware", $"{(o["Manufacturer"] as string)?.Trim()} {(o["SMBIOSBIOSVersion"] as string)?.Trim()}".Trim()));
        }
        catch { }
        info.Machine.Add(new("User", Environment.UserName));

        // ---------- GPU
        try
        {
            using var s = new ManagementObjectSearcher("SELECT Name, DriverVersion, AdapterRAM, VideoModeDescription, CurrentRefreshRate FROM Win32_VideoController");
            foreach (ManagementObject o in s.Get())
            {
                ulong ram = 0;
                try { ram = Convert.ToUInt64(o["AdapterRAM"] ?? 0UL); } catch { }
                info.Gpus.Add(new GpuInfo
                {
                    Name = (o["Name"] as string ?? "Display adapter").Trim(),
                    Driver = o["DriverVersion"] as string ?? "—",
                    Memory = ram > 0 ? Fmt.Bytes(ram) + (ram >= 0xFFF00000UL ? "+" : "") : "Shared",
                    Mode = o["VideoModeDescription"] as string ?? "—",
                });
            }
        }
        catch (Exception ex) { SettingsService.Log("WMI GPU: " + ex.Message); }

        // ---------- Displays (Win32)
        try { CollectDisplays(info); } catch (Exception ex) { SettingsService.Log("Displays: " + ex.Message); }

        return info;
    }

    private static void CollectDisplays(SystemInfo info)
    {
        var scale = new Dictionary<string, (uint Dpi, Native.RECT Rect)>(StringComparer.OrdinalIgnoreCase);
        Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr h, IntPtr hdc, ref Native.RECT r, IntPtr d) =>
        {
            var mi = new Native.MONITORINFOEX { cbSize = Marshal.SizeOf<Native.MONITORINFOEX>() };
            if (Native.GetMonitorInfo(h, ref mi))
            {
                uint dpi = 96;
                try { if (Native.GetDpiForMonitor(h, 0, out var dx, out _) == 0) dpi = dx; } catch { }
                scale[mi.szDevice] = (dpi, mi.rcMonitor);
            }
            return true;
        }, IntPtr.Zero);

        for (uint i = 0; ; i++)
        {
            var dd = new Native.DISPLAY_DEVICE { cb = Marshal.SizeOf<Native.DISPLAY_DEVICE>() };
            if (!Native.EnumDisplayDevices(null, i, ref dd, 0)) break;
            if ((dd.StateFlags & Native.DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) == 0) continue;

            var dm = new Native.DEVMODE { dmSize = (ushort)Marshal.SizeOf<Native.DEVMODE>() };
            string res = "—", hz = "—";
            if (Native.EnumDisplaySettings(dd.DeviceName, Native.ENUM_CURRENT_SETTINGS, ref dm))
            {
                res = $"{dm.dmPelsWidth} × {dm.dmPelsHeight}, {dm.dmBitsPerPel}-bit";
                hz = dm.dmDisplayFrequency > 1 ? $"{dm.dmDisplayFrequency} Hz" : "Default";
            }

            // Monitor friendly name
            string monitor = "";
            var mon = new Native.DISPLAY_DEVICE { cb = Marshal.SizeOf<Native.DISPLAY_DEVICE>() };
            if (Native.EnumDisplayDevices(dd.DeviceName, 0, ref mon, 0)) monitor = mon.DeviceString;

            string sc = scale.TryGetValue(dd.DeviceName, out var v) ? $"{Math.Round(v.Dpi / 96.0 * 100)}%" : "—";
            info.Displays.Add(new DisplayInfo
            {
                Name = string.IsNullOrWhiteSpace(monitor) ? dd.DeviceName.Replace(@"\\.\", "") : monitor,
                Adapter = dd.DeviceString,
                Resolution = res,
                Refresh = hz,
                Scale = sc,
                Primary = (dd.StateFlags & Native.DISPLAY_DEVICE_PRIMARY_DEVICE) != 0,
            });
        }
    }
}

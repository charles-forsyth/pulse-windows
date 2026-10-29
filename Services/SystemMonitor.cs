using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace Pulse.Services;

/// <summary>One immutable sample of live system telemetry. All values are measured, never synthesised.</summary>
public sealed class Snapshot
{
    public DateTime Timestamp { get; init; }
    public double Cpu { get; init; }                 // % total
    public double[] Cores { get; init; } = [];       // % per logical processor
    public ulong MemTotal { get; init; }
    public ulong MemAvailable { get; init; }
    public ulong MemUsed => MemTotal - MemAvailable;
    public double MemPercent => MemTotal == 0 ? 0 : MemUsed * 100.0 / MemTotal;
    public ulong CommitTotal { get; init; }
    public ulong CommitLimit { get; init; }
    public ulong SystemCache { get; init; }
    public bool DiskAvailable { get; init; }
    public double DiskRead { get; init; }            // bytes/s
    public double DiskWrite { get; init; }           // bytes/s
    public double DiskActive { get; init; }          // % active time
    public double NetDown { get; init; }             // bytes/s
    public double NetUp { get; init; }               // bytes/s
    public double NetLinkBps { get; init; }          // bits/s, sum of active adapters
    public double NetUtilization => NetLinkBps <= 0 ? 0 : Math.Min(100, Math.Max(NetDown, NetUp) * 8 * 100 / NetLinkBps);
    public int NetAdapters { get; init; }
    public uint Processes { get; init; }
    public uint Threads { get; init; }
    public uint Handles { get; init; }
    public TimeSpan Uptime { get; init; }
}

/// <summary>
/// Samples CPU (GetSystemTimes / NtQuerySystemInformation), memory (GlobalMemoryStatusEx / GetPerformanceInfo),
/// disk (PhysicalDisk performance counters) and network (NetworkInterface statistics) on a background task.
/// </summary>
public sealed class SystemMonitor : IDisposable
{
    public event Action<Snapshot>? Sampled;

    private CancellationTokenSource? _cts;
    private TimeSpan _interval = TimeSpan.FromSeconds(1);
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private ulong _prevIdle, _prevKernel, _prevUser;
    private long[] _prevCoreIdle = [], _prevCoreTotal = [];
    private long _prevRx, _prevTx;
    private double _prevNetT;

    private PerformanceCounter? _diskRead, _diskWrite, _diskIdle;

    public TimeSpan Interval
    {
        get => _interval;
        set => _interval = value < TimeSpan.FromMilliseconds(250) ? TimeSpan.FromMilliseconds(250) : value;
    }

    public string? DiskCounterError { get; private set; }

    public void Start()
    {
        if (_cts != null) return;
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        _ = Task.Run(() => LoopAsync(ct), ct);
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        // Prime deltas, then emit a first sample quickly so the UI is never empty for long.
        Prime();
        try { await Task.Delay(400, ct); } catch (OperationCanceledException) { return; }
        Emit();

        // PerformanceCounter construction can take a second or two the first time; do it off the hot path.
        InitDiskCounters();

        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(_interval, ct); }
            catch (OperationCanceledException) { break; }
            Emit();
        }
    }

    private void Emit()
    {
        try
        {
            var s = Sample();
            Sampled?.Invoke(s);
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Sample failed: " + ex);
        }
    }

    private void InitDiskCounters()
    {
        try
        {
            _diskRead = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total", readOnly: true);
            _diskWrite = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total", readOnly: true);
            _diskIdle = new PerformanceCounter("PhysicalDisk", "% Idle Time", "_Total", readOnly: true);
            _diskRead.NextValue(); _diskWrite.NextValue(); _diskIdle.NextValue();
        }
        catch (Exception ex)
        {
            DiskCounterError = ex.Message;
            _diskRead?.Dispose(); _diskWrite?.Dispose(); _diskIdle?.Dispose();
            _diskRead = _diskWrite = _diskIdle = null;
        }
    }

    private void Prime()
    {
        Native.GetSystemTimes(out var idle, out var kernel, out var user);
        _prevIdle = idle.Value; _prevKernel = kernel.Value; _prevUser = user.Value;
        ReadCores(out _prevCoreIdle, out _prevCoreTotal);
        ReadNet(out _prevRx, out _prevTx, out _, out _);
        _prevNetT = _clock.Elapsed.TotalSeconds;
    }

    private Snapshot Sample()
    {
        // ---- CPU total
        double cpu = 0;
        if (Native.GetSystemTimes(out var idle, out var kernel, out var user))
        {
            ulong dIdle = idle.Value - _prevIdle, dKernel = kernel.Value - _prevKernel, dUser = user.Value - _prevUser;
            ulong total = dKernel + dUser; // kernel time includes idle
            cpu = total == 0 ? 0 : Math.Clamp((total - dIdle) * 100.0 / total, 0, 100);
            _prevIdle = idle.Value; _prevKernel = kernel.Value; _prevUser = user.Value;
        }

        // ---- CPU per core
        double[] cores = [];
        if (ReadCores(out var coreIdle, out var coreTotal) && coreIdle.Length == _prevCoreIdle.Length)
        {
            cores = new double[coreIdle.Length];
            for (int i = 0; i < cores.Length; i++)
            {
                long dT = coreTotal[i] - _prevCoreTotal[i];
                long dI = coreIdle[i] - _prevCoreIdle[i];
                cores[i] = dT <= 0 ? 0 : Math.Clamp((dT - dI) * 100.0 / dT, 0, 100);
            }
        }
        _prevCoreIdle = coreIdle; _prevCoreTotal = coreTotal;

        // ---- Memory
        var ms = new Native.MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<Native.MEMORYSTATUSEX>() };
        Native.GlobalMemoryStatusEx(ref ms);

        ulong commitTotal = 0, commitLimit = 0, cache = 0; uint procs = 0, threads = 0, handles = 0;
        if (Native.GetPerformanceInfo(out var pi, (uint)Marshal.SizeOf<Native.PERFORMANCE_INFORMATION>()))
        {
            ulong page = pi.PageSize;
            commitTotal = (ulong)pi.CommitTotal * page;
            commitLimit = (ulong)pi.CommitLimit * page;
            cache = (ulong)pi.SystemCache * page;
            procs = pi.ProcessCount; threads = pi.ThreadCount; handles = pi.HandleCount;
        }

        // ---- Disk
        bool diskOk = false; double dr = 0, dw = 0, da = 0;
        try
        {
            if (_diskRead != null && _diskWrite != null && _diskIdle != null)
            {
                dr = _diskRead.NextValue();
                dw = _diskWrite.NextValue();
                da = Math.Clamp(100 - _diskIdle.NextValue(), 0, 100);
                diskOk = true;
            }
        }
        catch (Exception ex) { DiskCounterError = ex.Message; }

        // ---- Network
        double down = 0, up = 0;
        ReadNet(out long rx, out long tx, out double link, out int adapters);
        double now = _clock.Elapsed.TotalSeconds;
        double dt = now - _prevNetT;
        if (dt > 0 && rx >= _prevRx && tx >= _prevTx)
        {
            down = (rx - _prevRx) / dt;
            up = (tx - _prevTx) / dt;
        }
        _prevRx = rx; _prevTx = tx; _prevNetT = now;

        return new Snapshot
        {
            Timestamp = DateTime.Now,
            Cpu = cpu,
            Cores = cores,
            MemTotal = ms.ullTotalPhys,
            MemAvailable = ms.ullAvailPhys,
            CommitTotal = commitTotal,
            CommitLimit = commitLimit,
            SystemCache = cache,
            DiskAvailable = diskOk,
            DiskRead = dr,
            DiskWrite = dw,
            DiskActive = da,
            NetDown = down,
            NetUp = up,
            NetLinkBps = link,
            NetAdapters = adapters,
            Processes = procs,
            Threads = threads,
            Handles = handles,
            Uptime = TimeSpan.FromMilliseconds(Environment.TickCount64),
        };
    }

    private static unsafe bool ReadCores(out long[] idle, out long[] total)
    {
        int n = Environment.ProcessorCount;
        int size = Marshal.SizeOf<Native.SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION>();
        IntPtr buf = Marshal.AllocHGlobal(size * n);
        try
        {
            int status = Native.NtQuerySystemInformation(Native.SystemProcessorPerformanceInformation, buf, size * n, out int ret);
            if (status != 0 || ret < size) { idle = []; total = []; return false; }
            int count = ret / size;
            idle = new long[count]; total = new long[count];
            for (int i = 0; i < count; i++)
            {
                var p = Marshal.PtrToStructure<Native.SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION>(buf + i * size);
                idle[i] = p.IdleTime;
                total[i] = p.KernelTime + p.UserTime;
            }
            return true;
        }
        finally { Marshal.FreeHGlobal(buf); }
    }

    private static void ReadNet(out long rx, out long tx, out double linkBps, out int adapters)
    {
        rx = tx = 0; linkBps = 0; adapters = 0;
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
                var st = nic.GetIPStatistics();
                rx += st.BytesReceived;
                tx += st.BytesSent;
                if (nic.Speed > 0) linkBps += nic.Speed;
                adapters++;
            }
        }
        catch { /* adapters can disappear mid-enumeration */ }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _diskRead?.Dispose(); _diskWrite?.Dispose(); _diskIdle?.Dispose();
    }
}

using System.ComponentModel;
using System.Diagnostics;

namespace Pulse.Services;

public sealed record ProcInfo(int Pid, string Name, double Cpu, long WorkingSet, long PrivateBytes, int Threads, bool CpuKnown);

/// <summary>Computes per-process CPU% from TotalProcessorTime deltas between successive samples.</summary>
public sealed class ProcessSampler
{
    private Dictionary<int, (TimeSpan Cpu, DateTime Start)> _prev = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _prevT;

    public List<ProcInfo> Sample()
    {
        double now = _clock.Elapsed.TotalSeconds;
        double dt = now - _prevT;
        bool haveDelta = _prev.Count > 0 && dt > 0.05;
        int ncpu = Environment.ProcessorCount;

        var next = new Dictionary<int, (TimeSpan, DateTime)>();
        var list = new List<ProcInfo>(400);

        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (p.Id == 0) continue; // System Idle Process
                double cpu = 0; bool known = false;
                try
                {
                    var t = p.TotalProcessorTime;
                    DateTime start = default;
                    try { start = p.StartTime; } catch { }
                    next[p.Id] = (t, start);
                    if (haveDelta && _prev.TryGetValue(p.Id, out var prev) && prev.Start == start)
                    {
                        cpu = Math.Clamp((t - prev.Cpu).TotalSeconds / dt / ncpu * 100.0, 0, 100);
                    }
                    known = true;
                }
                catch (Win32Exception) { }
                catch (InvalidOperationException) { continue; } // exited

                long ws = 0, priv = 0; int threads = 0;
                try { ws = p.WorkingSet64; priv = p.PrivateMemorySize64; threads = p.Threads.Count; } catch { }

                list.Add(new ProcInfo(p.Id, p.ProcessName, cpu, ws, priv, threads, known));
            }
            finally { p.Dispose(); }
        }

        _prev = next;
        _prevT = now;
        return list;
    }
}

namespace Pulse.Services;

/// <summary>UI-thread store of the latest sample and rolling histories, shared by all pages.</summary>
public static class Telemetry
{
    public const int Capacity = 60;

    public static History Cpu { get; } = new(Capacity);
    public static History Memory { get; } = new(Capacity);
    public static History DiskRead { get; } = new(Capacity);
    public static History DiskWrite { get; } = new(Capacity);
    public static History DiskActive { get; } = new(Capacity);
    public static History NetDown { get; } = new(Capacity);
    public static History NetUp { get; } = new(Capacity);

    public static Snapshot? Latest { get; private set; }

    /// <summary>Raised on the UI thread after each new sample has been recorded.</summary>
    public static event Action? Updated;

    public static void Push(Snapshot s)
    {
        Latest = s;
        Cpu.Push(s.Cpu);
        Memory.Push(s.MemPercent);
        DiskRead.Push(s.DiskRead);
        DiskWrite.Push(s.DiskWrite);
        DiskActive.Push(s.DiskActive);
        NetDown.Push(s.NetDown);
        NetUp.Push(s.NetUp);
        Updated?.Invoke();
    }
}

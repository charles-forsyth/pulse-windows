namespace Pulse.Services;

/// <summary>Fixed-length rolling history of samples (oldest first). UI-thread only.</summary>
public sealed class History
{
    private readonly double[] _buf;
    private int _start;

    public History(int capacity) => _buf = new double[capacity];

    public int Capacity => _buf.Length;
    public int Count { get; private set; }

    public void Push(double v)
    {
        if (Count < _buf.Length)
        {
            _buf[(_start + Count) % _buf.Length] = v;
            Count++;
        }
        else
        {
            _buf[_start] = v;
            _start = (_start + 1) % _buf.Length;
        }
    }

    public double this[int i] => _buf[(_start + i) % _buf.Length];

    public double Last => Count == 0 ? 0 : this[Count - 1];

    public double Max()
    {
        double m = 0;
        for (int i = 0; i < Count; i++) m = Math.Max(m, this[i]);
        return m;
    }

    public double[] ToArray()
    {
        var a = new double[Count];
        for (int i = 0; i < Count; i++) a[i] = this[i];
        return a;
    }
}

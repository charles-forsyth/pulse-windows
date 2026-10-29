using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Pulse.Services;
using Windows.UI;

namespace Pulse.Views;

/// <summary>A reusable list row. Rows are recycled in place so the list never flickers or loses scroll position.</summary>
public sealed class ProcRow : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private static readonly Color[] Palette =
    [
        Color.FromArgb(255, 99, 102, 241), Color.FromArgb(255, 139, 92, 246), Color.FromArgb(255, 236, 72, 153),
        Color.FromArgb(255, 244, 63, 94), Color.FromArgb(255, 249, 115, 22), Color.FromArgb(255, 234, 179, 8),
        Color.FromArgb(255, 34, 197, 94), Color.FromArgb(255, 20, 184, 166), Color.FromArgb(255, 6, 182, 212),
        Color.FromArgb(255, 59, 130, 246),
    ];
    private static readonly Dictionary<int, SolidColorBrush> BrushCache = new();

    public int Pid { get; private set; } = -1;
    public string Name { get; private set; } = "";
    public string Initial { get; private set; } = "";
    public string PidText { get; private set; } = "";
    public string CpuText { get; private set; } = "";
    public double CpuBar { get; private set; }
    public string MemText { get; private set; } = "";
    public string PrivText { get; private set; } = "";
    public string ThreadsText { get; private set; } = "";
    public SolidColorBrush Avatar { get; private set; } = new(Color.FromArgb(255, 128, 128, 128));

    public void Set(ProcInfo p)
    {
        if (Pid != p.Pid || Name != p.Name)
        {
            Pid = p.Pid;
            Name = p.Name;
            Initial = string.IsNullOrEmpty(p.Name) ? "?" : char.ToUpperInvariant(p.Name[0]).ToString();
            PidText = p.Pid.ToString();
            int h = 0;
            foreach (var ch in p.Name.ToLowerInvariant()) h = h * 31 + ch;
            int idx = (int)((uint)h % Palette.Length);
            if (!BrushCache.TryGetValue(idx, out var b)) BrushCache[idx] = b = new SolidColorBrush(Palette[idx]);
            Avatar = b;
            Raise(nameof(Name)); Raise(nameof(Initial)); Raise(nameof(PidText)); Raise(nameof(Avatar));
        }
        Update(nameof(CpuText), CpuText, p.CpuKnown ? (p.Cpu >= 0.05 ? $"{p.Cpu:0.0}%" : "0%") : "—", v => CpuText = v);
        double bar = Math.Round(Math.Clamp(p.Cpu, 0, 100) / 100 * 104, 1);
        if (bar > 0 && bar < 2) bar = 2;
        if (Math.Abs(bar - CpuBar) > 0.05) { CpuBar = bar; Raise(nameof(CpuBar)); }
        Update(nameof(MemText), MemText, Fmt.Bytes(p.WorkingSet), v => MemText = v);
        Update(nameof(PrivText), PrivText, Fmt.Bytes(p.PrivateBytes), v => PrivText = v);
        Update(nameof(ThreadsText), ThreadsText, p.Threads.ToString(), v => ThreadsText = v);
    }

    private void Update(string name, string old, string value, Action<string> set)
    {
        if (old == value) return;
        set(value);
        Raise(name);
    }

    private void Raise([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public sealed partial class ProcessesPage : Page
{
    public ObservableCollection<ProcRow> Rows { get; } = new();

    private static readonly ProcessSampler Sampler = new();
    private List<ProcInfo> _latest = new();
    private DispatcherQueueTimer? _timer;
    private string _sort = "cpu";
    private bool _desc = true;
    private bool _busy;
    private bool _paused;
    private int _samples;

    public ProcessesPage()
    {
        InitializeComponent();
        UpdateHeaders();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _timer = DispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(Math.Max(1, SettingsService.Current.IntervalSeconds * 2));
        _timer.Tick += (_, _) => _ = RefreshAsync();
        _timer.Start();
        _ = RefreshAsync();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        _timer?.Stop();
        _timer = null;
    }

    private async Task RefreshAsync()
    {
        if (_busy || _paused) return;
        _busy = true;
        try
        {
            var list = await Task.Run(Sampler.Sample);
            _samples++;
            if (_samples == 1 && list.All(p => p.Cpu == 0))
            {
                // First sample only establishes a CPU-time baseline; take a quick second sample.
                await Task.Delay(700);
                list = await Task.Run(Sampler.Sample);
            }
            _latest = list;
            Apply();
            LoadingPanel.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            SettingsService.Log("Process refresh: " + ex.Message);
        }
        finally { _busy = false; }
    }

    private void Apply()
    {
        string q = Search.Text?.Trim() ?? "";
        IEnumerable<ProcInfo> items = _latest;
        if (q.Length > 0)
            items = items.Where(p => p.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || p.Pid.ToString().StartsWith(q));

        Func<ProcInfo, object> key = _sort switch
        {
            "name" => p => p.Name.ToLowerInvariant(),
            "pid" => p => p.Pid,
            "mem" => p => p.WorkingSet,
            "priv" => p => p.PrivateBytes,
            "threads" => p => p.Threads,
            _ => p => p.Cpu,
        };
        var sorted = (_desc ? items.OrderByDescending(key) : items.OrderBy(key))
            .ThenByDescending(p => p.WorkingSet)
            .ToList();

        // Recycle rows in place
        for (int i = 0; i < sorted.Count; i++)
        {
            if (i < Rows.Count) Rows[i].Set(sorted[i]);
            else { var r = new ProcRow(); r.Set(sorted[i]); Rows.Add(r); }
        }
        while (Rows.Count > sorted.Count) Rows.RemoveAt(Rows.Count - 1);

        Empty.Visibility = sorted.Count == 0 && _latest.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        double totalCpu = _latest.Sum(p => p.Cpu);
        long totalWs = _latest.Sum(p => p.WorkingSet);
        var top = _latest.OrderByDescending(p => p.Cpu).FirstOrDefault();
        Summary.Text = $"{_latest.Count} processes · {sorted.Count} shown · Σ CPU {Math.Min(totalCpu, 100):0}% · Σ working set {Fmt.Bytes(totalWs)}" +
                       (top != null && top.Cpu > 0.5 ? $" · busiest: {top.Name} ({top.Cpu:0.0}%)" : "") +
                       (_paused ? " · paused" : "");
    }

    private void Search_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) => Apply();

    private void Header_Click(object sender, RoutedEventArgs e)
    {
        var tag = (string)((FrameworkElement)sender).Tag;
        if (_sort == tag) _desc = !_desc;
        else { _sort = tag; _desc = tag is not ("name" or "pid"); }
        UpdateHeaders();
        Apply();
    }

    private void UpdateHeaders()
    {
        var map = new Dictionary<string, (HyperlinkButton B, string Label)>
        {
            ["name"] = (HdrName, "Name"), ["pid"] = (HdrPid, "PID"), ["cpu"] = (HdrCpu, "CPU"),
            ["mem"] = (HdrMem, "Memory"), ["priv"] = (HdrPriv, "Private"), ["threads"] = (HdrThreads, "Threads"),
        };
        foreach (var (k, v) in map)
            v.B.Content = k == _sort ? $"{v.Label} {(_desc ? "▾" : "▴")}" : v.Label;
    }

    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        _paused = PauseButton.IsChecked == true;
        PauseIcon.Glyph = _paused ? "\uE768" : "\uE769";
        ToolTipService.SetToolTip(PauseButton, _paused ? "Resume updates" : "Pause updates");
        if (!_paused) _ = RefreshAsync(); else Apply();
    }
}

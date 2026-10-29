using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Pulse.Services;
using Windows.UI;

namespace Pulse.Views;

public sealed partial class OverviewPage : Page
{
    private static readonly Color MemColor = Color.FromArgb(255, 139, 92, 246);    // violet
    private static readonly Color MemColor2 = Color.FromArgb(255, 192, 132, 252);
    private static readonly Color DiskColor = Color.FromArgb(255, 16, 185, 129);   // emerald
    private static readonly Color DiskColor2 = Color.FromArgb(255, 245, 158, 11);  // amber
    private static readonly Color NetColor = Color.FromArgb(255, 6, 182, 212);     // cyan
    private static readonly Color NetColor2 = Color.FromArgb(255, 236, 72, 153);   // pink

    private bool _entered;

    public OverviewPage()
    {
        InitializeComponent();

        MemGauge.Color = MemColor; MemGauge.Color2 = MemColor2;
        DiskGauge.Color = DiskColor; DiskGauge.Color2 = Color.FromArgb(255, 52, 211, 153);
        NetGauge.Color = NetColor; NetGauge.Color2 = Color.FromArgb(255, 103, 232, 249);

        MemChart.ColorA = MemColor;
        DiskChart.ColorA = DiskColor; DiskChart.ColorB = DiskColor2; DiskChart.LabelFormatter = v => Fmt.Rate(v);
        NetChart.ColorA = NetColor; NetChart.ColorB = NetColor2; NetChart.LabelFormatter = v => Fmt.Rate(v);
        DiskReadDot.Fill = new SolidColorBrush(DiskColor); DiskWriteDot.Fill = new SolidColorBrush(DiskColor2);
        NetDownDot.Fill = new SolidColorBrush(NetColor); NetUpDot.Fill = new SolidColorBrush(NetColor2);
        ApplyAccent();

        Subtitle.Text = $"{Environment.MachineName} · {SystemInfoService.CpuName} · {Environment.ProcessorCount} logical processors";
        CoresLabel.Text = $"Logical processors ({Environment.ProcessorCount})";
    }

    private void ApplyAccent()
    {
        var a = MainWindow.CurrentAccent;
        CpuGauge.Color = a;
        CpuGauge.Color2 = Lighten(a, 0.35);
        CpuChart.ColorA = a;
        Cores.Color = a;
    }

    private static Color Lighten(Color c, double t) =>
        Color.FromArgb(255, (byte)(c.R + (255 - c.R) * t), (byte)(c.G + (255 - c.G) * t), (byte)(c.B + (255 - c.B) * t));

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Telemetry.Updated += OnUpdated;
        MainWindow.AccentChanged += ApplyAccent;
        ApplyAccent();
        if (Telemetry.Latest != null) Render(animate: false);
        if (!_entered)
        {
            _entered = true;
            Stagger(GaugeGrid);
            Stagger(ChartGrid, 160);
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        Telemetry.Updated -= OnUpdated;
        MainWindow.AccentChanged -= ApplyAccent;
    }

    /// <summary>Staggered fade/slide-in of cards on first show.</summary>
    private static void Stagger(Panel panel, int baseDelay = 0)
    {
        int i = 0;
        foreach (var child in panel.Children)
        {
            var tt = new TranslateTransform { Y = 24 };
            child.RenderTransform = tt;
            child.Opacity = 0;
            var sb = new Storyboard();
            var begin = TimeSpan.FromMilliseconds(baseDelay + i * 60);
            var fade = new DoubleAnimation { To = 1, Duration = TimeSpan.FromMilliseconds(420), BeginTime = begin };
            var slide = new DoubleAnimation { To = 0, Duration = TimeSpan.FromMilliseconds(520), BeginTime = begin, EasingFunction = new ExponentialEase { Exponent = 5, EasingMode = EasingMode.EaseOut } };
            Storyboard.SetTarget(fade, child); Storyboard.SetTargetProperty(fade, "Opacity");
            Storyboard.SetTarget(slide, tt); Storyboard.SetTargetProperty(slide, "Y");
            sb.Children.Add(fade); sb.Children.Add(slide);
            sb.Begin();
            i++;
        }
    }

    private void OnUpdated() => Render(animate: true);

    private void Render(bool animate)
    {
        var s = Telemetry.Latest;
        if (s == null) return;
        var dur = animate ? TimeSpan.FromSeconds(Math.Min(SettingsService.Current.IntervalSeconds, 2) * 0.95) : TimeSpan.Zero;

        // Header stats
        UptimeText.Text = Fmt.Uptime(s.Uptime);
        ProcText.Text = s.Processes.ToString("N0");
        ThreadText.Text = s.Threads.ToString("N0");
        HandleText.Text = s.Handles.ToString("N0");

        // CPU
        CpuGauge.Value = s.Cpu;
        CpuGaugeText.Text = $"{s.Cpu:0}%";
        CpuValue.Text = $"{s.Cpu:0.0}%";
        int busy = s.Cores.Count(c => c > 50);
        CpuDetail.Text = s.Cores.Length > 0 ? $"{s.Cores.Length} threads · {busy} busy · peak {s.Cores.Max():0}%" : $"{Environment.ProcessorCount} threads";
        CpuChart.Update(Telemetry.Cpu.ToArray(), null, dur);
        CpuChartValue.Text = $"{s.Cpu:0.0}%";
        Cores.Update(s.Cores);

        // Memory
        MemGauge.Value = s.MemPercent;
        MemGaugeText.Text = $"{s.MemPercent:0}%";
        MemValue.Text = Fmt.Bytes(s.MemUsed);
        MemDetail.Text = $"of {Fmt.Bytes(s.MemTotal)} · {Fmt.Bytes(s.MemAvailable)} free";
        MemChart.Update(Telemetry.Memory.ToArray(), null, dur);
        MemChartValue.Text = $"{s.MemPercent:0.0}%";
        MemAvail.Text = Fmt.Bytes(s.MemAvailable);
        MemCommit.Text = s.CommitLimit > 0 ? $"{Fmt.Bytes(s.CommitTotal)} / {Fmt.Bytes(s.CommitLimit)}" : "—";
        MemCache.Text = Fmt.Bytes(s.SystemCache);

        // Disk
        if (s.DiskAvailable)
        {
            DiskGauge.Value = s.DiskActive;
            DiskGaugeText.Text = $"{s.DiskActive:0}%";
            DiskValue.Text = Fmt.Rate(s.DiskRead + s.DiskWrite);
            DiskDetail.Text = $"R {Fmt.Rate(s.DiskRead)} · W {Fmt.Rate(s.DiskWrite)}";
            DiskReadText.Text = $"Read {Fmt.Rate(s.DiskRead)}";
            DiskWriteText.Text = $"Write {Fmt.Rate(s.DiskWrite)}";
            DiskChart.Update(Telemetry.DiskRead.ToArray(), Telemetry.DiskWrite.ToArray(), dur);
            DiskNote.Text = $"Physical disks (all) · active time {s.DiskActive:0}%";
        }
        else
        {
            DiskGaugeText.Text = "—";
            DiskValue.Text = "—";
            var err = App.MainWindow?.Monitor.DiskCounterError;
            DiskDetail.Text = err != null ? "Counters unavailable" : "Initialising counters…";
            DiskNote.Text = err != null ? $"Disk performance counters unavailable: {err}" : "Initialising performance counters…";
        }

        // Network
        double total = s.NetDown + s.NetUp;
        NetGauge.Value = s.NetUtilization;
        NetGaugeText.Text = s.NetUtilization < 1 && total > 0 ? "<1%" : $"{s.NetUtilization:0}%";
        NetValue.Text = Fmt.Rate(total);
        NetDetail.Text = $"↓ {Fmt.Rate(s.NetDown)} · ↑ {Fmt.Rate(s.NetUp)}";
        NetDownText.Text = $"Down {Fmt.Rate(s.NetDown)}";
        NetUpText.Text = $"Up {Fmt.Rate(s.NetUp)}";
        NetChart.Update(Telemetry.NetDown.ToArray(), Telemetry.NetUp.ToArray(), dur);
        NetNote.Text = $"{s.NetAdapters} active adapter{(s.NetAdapters == 1 ? "" : "s")} · link speed {Fmt.Bits(s.NetLinkBps)} · gauge shows link utilisation";
    }
}

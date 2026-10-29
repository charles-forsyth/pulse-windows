using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Pulse.Services;

namespace Pulse.Views;

public sealed partial class SystemPage : Page
{
    public SystemPage()
    {
        InitializeComponent();
        HostName.Text = Environment.MachineName;
        CpuName.Text = SystemInfoService.CpuName;
        Cards.ChildrenTransitions = new TransitionCollection { new EntranceThemeTransition { IsStaggeringEnabled = true } };
        GpuCards.ChildrenTransitions = new TransitionCollection { new EntranceThemeTransition { IsStaggeringEnabled = true } };
        DisplayCards.ChildrenTransitions = new TransitionCollection { new EntranceThemeTransition { IsStaggeringEnabled = true } };
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Telemetry.Updated += Tick;
        Tick();
        try
        {
            var info = await SystemInfoService.GetAsync();
            Populate(info);
        }
        catch (Exception ex)
        {
            OsName.Text = "Could not read system information: " + ex.Message;
        }
        finally { Progress.Visibility = Visibility.Collapsed; }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        Telemetry.Updated -= Tick;
    }

    private void Tick()
    {
        var up = TimeSpan.FromMilliseconds(Environment.TickCount64);
        Uptime.Text = Fmt.Uptime(up);
        BootTime.Text = "since " + (DateTime.Now - up).ToString("ddd d MMM, HH:mm");
    }

    private void Populate(SystemInfo info)
    {
        OsName.Text = info.OsTitle;
        CpuName.Text = $"{info.CpuTitle} · {Environment.ProcessorCount} logical processors";
        Fill(OsRows, info.Os);
        Fill(CpuRows, info.Cpu);
        Fill(MachineRows, info.Machine);

        GpuCards.Children.Clear();
        if (info.Gpus.Count == 0) GpuCards.Children.Add(Note("No display adapters reported by WMI."));
        foreach (var g in info.Gpus)
        {
            GpuCards.Children.Add(MakeCard("\uE7F4", g.Name, [
                new("Driver", g.Driver),
                new("Dedicated memory", g.Memory),
                new("Current mode", g.Mode),
            ]));
        }

        DisplayCards.Children.Clear();
        if (info.Displays.Count == 0) DisplayCards.Children.Add(Note("No attached displays found."));
        int i = 1;
        foreach (var d in info.Displays)
        {
            DisplayCards.Children.Add(MakeCard("\uE7F4", $"Display {i++}{(d.Primary ? " · Primary" : "")}", [
                new("Monitor", d.Name),
                new("Resolution", d.Resolution),
                new("Refresh rate", d.Refresh),
                new("Scale", d.Scale),
                new("Adapter", d.Adapter),
            ]));
        }
    }

    private static void Fill(StackPanel panel, IEnumerable<InfoRow> rows)
    {
        panel.Children.Clear();
        bool first = true;
        foreach (var r in rows)
        {
            if (!first)
                panel.Children.Add(new Border { Height = 1, Background = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"], Margin = new Thickness(0, 2, 0, 2) });
            first = false;
            var g = new Grid { ColumnSpacing = 16, Padding = new Thickness(0, 6, 0, 6) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.Children.Add(new TextBlock
            {
                Text = r.Label,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            });
            var v = new TextBlock { Text = r.Value, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true, FontWeight = FontWeights.SemiBold };
            Grid.SetColumn(v, 1);
            g.Children.Add(v);
            panel.Children.Add(g);
        }
    }

    private static Border MakeCard(string glyph, string title, List<InfoRow> rows)
    {
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        header.Children.Add(new FontIcon { Glyph = glyph, FontSize = 16, Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"] });
        header.Children.Add(new TextBlock { Text = title, Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"], TextTrimming = TextTrimming.CharacterEllipsis });
        var body = new StackPanel();
        Fill(body, rows);
        var sp = new StackPanel { Spacing = 12 };
        sp.Children.Add(header);
        sp.Children.Add(body);
        return new Border { Style = (Style)Application.Current.Resources["Card"], Child = sp };
    }

    private static UIElement Note(string text) => new TextBlock { Text = text, Style = (Style)Application.Current.Resources["Caption"] };
}

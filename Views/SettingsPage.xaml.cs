using System.Runtime.InteropServices;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Pulse.Services;
using Windows.UI;

namespace Pulse.Views;

public sealed partial class SettingsPage : Page
{
    private static readonly (string Name, string Value)[] Accents =
    [
        ("Windows accent", "System"),
        ("Blue", "#0078D4"), ("Indigo", "#4F46E5"), ("Violet", "#7C3AED"), ("Pink", "#DB2777"),
        ("Red", "#DC2626"), ("Orange", "#EA580C"), ("Amber", "#D97706"), ("Green", "#16A34A"),
        ("Teal", "#0D9488"), ("Cyan", "#0891B2"), ("Slate", "#475569"),
    ];

    private bool _loading = true;

    public SettingsPage()
    {
        InitializeComponent();
        var s = SettingsService.Current;
        Select(ThemeBox, s.Theme);
        Select(BackdropBox, s.Backdrop);
        Select(RateBox, s.IntervalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (RateBox.SelectedIndex < 0) RateBox.SelectedIndex = 1;

        foreach (var (name, value) in Accents)
        {
            var color = value == "System"
                ? new Windows.UI.ViewManagement.UISettings().GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent)
                : (MainWindow.TryParseColor(value, out var c) ? c : Color.FromArgb(255, 0, 120, 212));
            var swatch = new Grid { Width = 40, Height = 40, Tag = value };
            swatch.Children.Add(new Ellipse { Fill = new SolidColorBrush(color), Width = 32, Height = 32 });
            if (value == "System")
                swatch.Children.Add(new FontIcon { Glyph = "\uE770", FontSize = 13, Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)) });
            ToolTipService.SetToolTip(swatch, name);
            AccentGrid.Items.Add(swatch);
            if (value.Equals(s.Accent, StringComparison.OrdinalIgnoreCase)) AccentGrid.SelectedItem = swatch;
        }

        var ver = typeof(App).Assembly.GetName().Version;
        AboutText.Text = $"Version {ver?.ToString(3)} · {RuntimeInformation.ProcessArchitecture} · {RuntimeInformation.FrameworkDescription} · WinUI 3 (Windows App SDK, self-contained)";
        PathText.Text = $"Settings: {System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pulse", "settings.json")}";
        _loading = false;
    }

    private static void Select(ComboBox box, string tag)
    {
        foreach (var item in box.Items.OfType<ComboBoxItem>())
            if ((string)item.Tag == tag) { box.SelectedItem = item; return; }
        box.SelectedIndex = 0;
    }

    private static string? TagOf(ComboBox box) => (box.SelectedItem as ComboBoxItem)?.Tag as string;

    private void Theme_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || TagOf(ThemeBox) is not { } t) return;
        SettingsService.Current.Theme = t;
        SettingsService.Save();
    }

    private void Backdrop_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || TagOf(BackdropBox) is not { } t) return;
        SettingsService.Current.Backdrop = t;
        SettingsService.Save();
    }

    private void Rate_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || TagOf(RateBox) is not { } t) return;
        SettingsService.Current.IntervalSeconds = double.Parse(t, System.Globalization.CultureInfo.InvariantCulture);
        SettingsService.Save();
    }

    private void Accent_Click(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is FrameworkElement fe && fe.Tag is string v)
        {
            SettingsService.Current.Accent = v;
            SettingsService.Save();
        }
    }
}

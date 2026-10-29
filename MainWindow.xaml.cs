using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Pulse.Services;
using Pulse.Views;
using Windows.UI;
using WinRT.Interop;

namespace Pulse;

public sealed partial class MainWindow : Window
{
    public SystemMonitor Monitor { get; } = new();
    private Type? _currentPage;

    public MainWindow()
    {
        InitializeComponent();

        // ---- Window chrome
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.Title = "Pulse";
        SetWindowIcon();
        ArchBadge.Text = RuntimeInformation.ProcessArchitecture.ToString().ToUpperInvariant();

        var hwnd = WindowNative.GetWindowHandle(this);
        double scale = Native.GetDpiForWindow(hwnd) / 96.0;
        if (scale <= 0) scale = 1;
        AppWindow.Resize(new Windows.Graphics.SizeInt32((int)(1280 * scale), (int)(840 * scale)));
        CenterOnScreen();
        if (AppWindow.Presenter is OverlappedPresenter op)
        {
            op.PreferredMinimumWidth = (int)(560 * scale);
            op.PreferredMinimumHeight = (int)(480 * scale);
        }

        AppTitleBar.SizeChanged += (_, _) => UpdateInsets();
        Activated += (_, e) =>
        {
            AppTitleBar.Opacity = e.WindowActivationState == WindowActivationState.Deactivated ? 0.6 : 1.0;
        };

        // ---- Theme / backdrop
        ApplyBackdrop();
        ApplyTheme();
        SettingsService.Changed += () => { ApplyTheme(); ApplyBackdrop(); Monitor.Interval = TimeSpan.FromSeconds(SettingsService.Current.IntervalSeconds); };
        RootGrid.ActualThemeChanged += (_, _) => UpdateCaptionButtons();

        // ---- Navigation transitions
        ContentFrame.ContentTransitions = new TransitionCollection { new NavigationThemeTransition() };
        Navigate(typeof(OverviewPage), new EntranceNavigationTransitionInfo());
        Nav.Loaded += (_, _) => SelectStartPage();

        // ---- Telemetry
        Monitor.Interval = TimeSpan.FromSeconds(SettingsService.Current.IntervalSeconds);
        Monitor.Sampled += s => DispatcherQueue.TryEnqueue(() =>
        {
            Telemetry.Push(s);
            LiveText.Text = $"Live · {s.Cpu:0}% CPU · {s.MemPercent:0}% RAM";
            PulseDot();
        });
        Monitor.Start();

        Closed += (_, _) => Monitor.Dispose();
    }

    private void PulseDot()
    {
        var sb = new Storyboard();
        var a = new DoubleAnimation { From = 1, To = 0.25, Duration = TimeSpan.FromMilliseconds(450), AutoReverse = true, EasingFunction = new SineEase() };
        Storyboard.SetTarget(a, LiveDot);
        Storyboard.SetTargetProperty(a, "Opacity");
        sb.Children.Add(a);
        sb.Begin();
    }

    private void SetWindowIcon()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (exe == null) return;
            var large = new IntPtr[1];
            if (Native.ExtractIconEx(exe, 0, large, null, 1) > 0 && large[0] != IntPtr.Zero)
                AppWindow.SetIcon(Win32Interop.GetIconIdFromIcon(large[0]));
        }
        catch (Exception ex) { SettingsService.Log("Icon: " + ex.Message); }
    }

    private void CenterOnScreen()
    {
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);
        if (area == null) return;
        var wa = area.WorkArea;
        var sz = AppWindow.Size;
        int w = Math.Min(sz.Width, wa.Width), h = Math.Min(sz.Height, wa.Height);
        AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(wa.X + (wa.Width - w) / 2, wa.Y + (wa.Height - h) / 2, w, h));
    }

    private void UpdateInsets()
    {
        double s = AppTitleBar.XamlRoot?.RasterizationScale ?? 1;
        if (s <= 0) s = 1;
        LeftInset.Width = new GridLength(AppWindow.TitleBar.LeftInset / s);
        RightInset.Width = new GridLength(AppWindow.TitleBar.RightInset / s);
    }

    // ------------------------------------------------------------------ Navigation

    /// <summary>Supports `Pulse.exe --page processes|system|settings` for deep-linking.</summary>
    private void SelectStartPage()
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.FindIndex(args, a => a.Equals("--page", StringComparison.OrdinalIgnoreCase));
        if (i < 0 || i + 1 >= args.Length) return;
        string tag = args[i + 1].ToLowerInvariant();
        if (tag == "settings") { Nav.SelectedItem = Nav.SettingsItem; return; }
        foreach (var item in Nav.MenuItems.OfType<NavigationViewItem>())
            if ((item.Tag as string) == tag) { Nav.SelectedItem = item; return; }
    }

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        Type page = args.IsSettingsSelected ? typeof(SettingsPage) : (args.SelectedItemContainer?.Tag as string) switch
        {
            "processes" => typeof(ProcessesPage),
            "system" => typeof(SystemPage),
            _ => typeof(OverviewPage),
        };
        Navigate(page, args.RecommendedNavigationTransitionInfo ?? new EntranceNavigationTransitionInfo());
    }

    private void Navigate(Type page, NavigationTransitionInfo info)
    {
        if (_currentPage == page) return;
        _currentPage = page;
        ContentFrame.Navigate(page, null, info);
        ContentFrame.BackStack.Clear();
    }

    // ------------------------------------------------------------------ Theme

    public void ApplyTheme()
    {
        var s = SettingsService.Current;
        ApplyAccent(s.Accent);
        var theme = s.Theme switch { "Light" => ElementTheme.Light, "Dark" => ElementTheme.Dark, _ => ElementTheme.Default };
        // Toggle through a different value so re-applied accent resources are picked up by all ThemeResources.
        RootGrid.RequestedTheme = theme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;
        RootGrid.RequestedTheme = theme;
        UpdateCaptionButtons();
    }

    private static Color? _systemAccent;

    private static void ApplyAccent(string accent)
    {
        var res = Application.Current.Resources;
        var ui = new Windows.UI.ViewManagement.UISettings();
        _systemAccent ??= ui.GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent);

        Color baseC;
        Color l1, l2, l3, d1, d2, d3;
        if (accent == "System" || !TryParseColor(accent, out baseC))
        {
            baseC = ui.GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent);
            l1 = ui.GetColorValue(Windows.UI.ViewManagement.UIColorType.AccentLight1);
            l2 = ui.GetColorValue(Windows.UI.ViewManagement.UIColorType.AccentLight2);
            l3 = ui.GetColorValue(Windows.UI.ViewManagement.UIColorType.AccentLight3);
            d1 = ui.GetColorValue(Windows.UI.ViewManagement.UIColorType.AccentDark1);
            d2 = ui.GetColorValue(Windows.UI.ViewManagement.UIColorType.AccentDark2);
            d3 = ui.GetColorValue(Windows.UI.ViewManagement.UIColorType.AccentDark3);
        }
        else
        {
            l1 = Mix(baseC, Colors.White, 0.25); l2 = Mix(baseC, Colors.White, 0.45); l3 = Mix(baseC, Colors.White, 0.65);
            d1 = Mix(baseC, Colors.Black, 0.2); d2 = Mix(baseC, Colors.Black, 0.4); d3 = Mix(baseC, Colors.Black, 0.6);
        }
        res["SystemAccentColor"] = baseC;
        res["SystemAccentColorLight1"] = l1;
        res["SystemAccentColorLight2"] = l2;
        res["SystemAccentColorLight3"] = l3;
        res["SystemAccentColorDark1"] = d1;
        res["SystemAccentColorDark2"] = d2;
        res["SystemAccentColorDark3"] = d3;
        AccentChanged?.Invoke();
    }

    public static event Action? AccentChanged;

    public static Color CurrentAccent =>
        Application.Current.Resources.TryGetValue("SystemAccentColor", out var v) && v is Color c ? c : Color.FromArgb(255, 0, 120, 212);

    public static bool TryParseColor(string s, out Color c)
    {
        c = default;
        if (s.Length != 7 || s[0] != '#') return false;
        try
        {
            c = Color.FromArgb(255, Convert.ToByte(s[1..3], 16), Convert.ToByte(s[3..5], 16), Convert.ToByte(s[5..7], 16));
            return true;
        }
        catch { return false; }
    }

    private static Color Mix(Color a, Color b, double t) => Color.FromArgb(255,
        (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));

    private void UpdateCaptionButtons()
    {
        var tb = AppWindow.TitleBar;
        bool dark = RootGrid.ActualTheme == ElementTheme.Dark;
        tb.ButtonBackgroundColor = Colors.Transparent;
        tb.ButtonInactiveBackgroundColor = Colors.Transparent;
        tb.ButtonForegroundColor = dark ? Colors.White : Colors.Black;
        tb.ButtonInactiveForegroundColor = dark ? Color.FromArgb(0x80, 255, 255, 255) : Color.FromArgb(0x80, 0, 0, 0);
        tb.ButtonHoverBackgroundColor = dark ? Color.FromArgb(0x18, 255, 255, 255) : Color.FromArgb(0x10, 0, 0, 0);
        tb.ButtonHoverForegroundColor = tb.ButtonForegroundColor;
        tb.ButtonPressedBackgroundColor = dark ? Color.FromArgb(0x10, 255, 255, 255) : Color.FromArgb(0x08, 0, 0, 0);
        tb.ButtonPressedForegroundColor = tb.ButtonForegroundColor;
    }

    private string? _appliedBackdrop;

    private void ApplyBackdrop()
    {
        var want = SettingsService.Current.Backdrop;
        if (want == _appliedBackdrop) return;
        _appliedBackdrop = want;
        try
        {
            if (want == "Acrylic" && DesktopAcrylicController.IsSupported())
                SystemBackdrop = new DesktopAcrylicBackdrop();
            else if (MicaController.IsSupported())
                SystemBackdrop = new MicaBackdrop { Kind = want == "MicaAlt" ? MicaKind.BaseAlt : MicaKind.Base };
            else
                SystemBackdrop = new DesktopAcrylicBackdrop();
        }
        catch (Exception ex) { SettingsService.Log("Backdrop: " + ex.Message); }
    }
}

using System.Numerics;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Pulse.Controls;

/// <summary>Per-logical-core load bars. Heights animate through UIElement.Scale + ScaleTransition (compositor-driven).</summary>
public sealed partial class CoreBars : Grid
{
    private readonly List<Border> _bars = new();
    private Color _color = Color.FromArgb(255, 0, 120, 212);

    public Color Color
    {
        get => _color;
        set { _color = value; foreach (var b in _bars) b.Background = Brush(); }
    }

    public CoreBars()
    {
        Height = 44;
        ColumnSpacing = 3;

    }

    private Brush Brush() => new LinearGradientBrush
    {
        StartPoint = new Windows.Foundation.Point(0, 1),
        EndPoint = new Windows.Foundation.Point(0, 0),
        GradientStops =
        {
            new GradientStop { Color = Color.FromArgb(0xB0, _color.R, _color.G, _color.B), Offset = 0 },
            new GradientStop { Color = _color, Offset = 1 },
        },
    };

    public void Update(double[] cores)
    {
        if (cores.Length == 0) return;
        if (_bars.Count != cores.Length) Build(cores.Length);
        for (int i = 0; i < cores.Length; i++)
        {
            float s = (float)Math.Clamp(cores[i] / 100.0, 0.03, 1);
            _bars[i].Scale = new Vector3(1, s, 1);
            ToolTipService.SetToolTip(_bars[i].Parent as UIElement, $"CPU {i}: {cores[i]:0}%");
        }
    }

    private void Build(int n)
    {
        Children.Clear();
        ColumnDefinitions.Clear();
        _bars.Clear();
        for (int i = 0; i < n; i++)
        {
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var track = new Border
            {
                CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush(Color.FromArgb(0x22, 0x80, 0x80, 0x80)),
            };
            var bar = new Border
            {
                CornerRadius = new CornerRadius(3),
                Background = Brush(),
                ScaleTransition = new Vector3Transition { Duration = TimeSpan.FromMilliseconds(600) },
                Scale = new Vector3(1, 0.03f, 1),
            };
            bar.SizeChanged += (o, e) => ((Border)o).CenterPoint = new Vector3(0, (float)e.NewSize.Height, 0);
            var cell = new Grid();
            cell.Children.Add(track);
            cell.Children.Add(bar);
            Grid.SetColumn(cell, i);
            Children.Add(cell);
            _bars.Add(bar);
        }
    }
}

using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Path = Microsoft.UI.Xaml.Shapes.Path;
using Windows.Foundation;
using Windows.UI;

namespace Pulse.Controls;

/// <summary>270° ring gauge drawn with XAML arcs; value changes are eased per-frame.</summary>
public sealed partial class RingGauge : Grid
{
    private readonly Path _track = new(), _value = new();
    private double _from, _to, _current;
    private long _startTicks;
    private bool _animating;
    private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(700);

    private const double StartAngle = 135, Sweep = 270;

    public double Thickness { get; set; } = 10;

    private Color _color = Color.FromArgb(255, 0, 120, 212);
    private Color _color2 = Color.FromArgb(255, 0, 180, 255);

    public Color Color { get => _color; set { _color = value; ApplyBrush(); } }
    public Color Color2 { get => _color2; set { _color2 = value; ApplyBrush(); } }

    public RingGauge()
    {
        _track.Stroke = new SolidColorBrush(Color.FromArgb(0x2A, 0x80, 0x80, 0x80));
        foreach (var p in new[] { _track, _value })
        {
            p.StrokeStartLineCap = PenLineCap.Round;
            p.StrokeEndLineCap = PenLineCap.Round;
        }
        Children.Add(_track);
        Children.Add(_value);
        ApplyBrush();
        SizeChanged += (_, _) => Draw();
        Unloaded += (_, _) => StopAnim();
    }

    private void ApplyBrush()
    {
        _value.Stroke = new LinearGradientBrush
        {
            StartPoint = new Point(0, 1),
            EndPoint = new Point(1, 0),
            GradientStops =
            {
                new GradientStop { Color = _color, Offset = 0 },
                new GradientStop { Color = _color2, Offset = 1 },
            },
        };
    }

    /// <summary>Value in percent, 0..100.</summary>
    public double Value
    {
        get => _to;
        set
        {
            value = Math.Clamp(double.IsNaN(value) ? 0 : value, 0, 100);
            _from = _current;
            _to = value;
            _startTicks = Environment.TickCount64;
            if (!_animating)
            {
                _animating = true;
                CompositionTarget.Rendering += OnRendering;
            }
        }
    }

    private void StopAnim()
    {
        if (_animating) CompositionTarget.Rendering -= OnRendering;
        _animating = false;
    }

    private void OnRendering(object? sender, object e)
    {
        double t = Math.Clamp((Environment.TickCount64 - _startTicks) / Duration.TotalMilliseconds, 0, 1);
        double eased = 1 - Math.Pow(1 - t, 3);
        _current = _from + (_to - _from) * eased;
        Draw();
        if (t >= 1) StopAnim();
    }

    private void Draw()
    {
        double size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0) return;
        _track.StrokeThickness = _value.StrokeThickness = Thickness;
        double r = (size - Thickness) / 2;
        var c = new Point(ActualWidth / 2, ActualHeight / 2);
        _track.Data = Arc(c, r, StartAngle, Sweep);
        double sweep = Sweep * _current / 100.0;
        if (sweep < 0.5) { _value.Opacity = 0; _value.Data = Arc(c, r, StartAngle, 0.5); }
        else { _value.Opacity = 1; _value.Data = Arc(c, r, StartAngle, sweep); }
    }

    private static Geometry Arc(Point c, double r, double start, double sweep)
    {
        static Point P(Point c, double r, double deg)
        {
            double rad = deg * Math.PI / 180;
            return new Point(c.X + r * Math.Cos(rad), c.Y + r * Math.Sin(rad));
        }
        var fig = new PathFigure { StartPoint = P(c, r, start), IsClosed = false, IsFilled = false };
        fig.Segments.Add(new ArcSegment
        {
            Point = P(c, r, start + sweep),
            Size = new Size(r, r),
            IsLargeArc = sweep > 180,
            SweepDirection = SweepDirection.Clockwise,
        });
        return new PathGeometry { Figures = { fig } };
    }
}

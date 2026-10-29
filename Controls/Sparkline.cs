using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using Path = Microsoft.UI.Xaml.Shapes.Path;
using Windows.Foundation;
using Windows.UI;

namespace Pulse.Controls;

/// <summary>
/// Smooth scrolling area chart drawn with XAML shapes. Supports one or two series.
/// Each update rebuilds the geometry one step to the right and slides it into place with an
/// independent (compositor-thread) TranslateTransform animation, giving a continuous scroll.
/// </summary>
public sealed partial class Sparkline : Grid
{
    private readonly Canvas _grid = new();
    private readonly Canvas _plot = new();
    private readonly Path _fillA = new(), _lineA = new(), _fillB = new(), _lineB = new();
    private readonly TranslateTransform _shift = new();
    private readonly Ellipse _dotA = new() { Width = 8, Height = 8 }, _dotB = new() { Width = 8, Height = 8 };
    private readonly TextBlock _maxLabel = new() { FontSize = 11, Opacity = 0.55, Margin = new Thickness(2, 0, 0, 0) };
    private readonly RectangleGeometry _clip = new();
    private Storyboard? _sb;

    private double[] _a = [], _b = [];
    private double _displayMax = 1;

    public int Capacity { get; set; } = 60;
    /// <summary>Fixed Y maximum (e.g. 100 for percentages). 0 = auto-scale.</summary>
    public double FixedMax { get; set; }
    /// <summary>Formats the auto-scale maximum label.</summary>
    public Func<double, string>? LabelFormatter { get; set; }

    private Color _colorA = Color.FromArgb(255, 0, 120, 212);
    private Color _colorB = Color.FromArgb(255, 255, 140, 0);

    public Color ColorA { get => _colorA; set { _colorA = value; ApplyBrushes(); } }
    public Color ColorB { get => _colorB; set { _colorB = value; ApplyBrushes(); } }

    public Sparkline()
    {
        MinHeight = 60;
        _plot.RenderTransform = _shift;
        foreach (var p in new[] { _lineA, _lineB })
        {
            p.StrokeThickness = 2;
            p.StrokeLineJoin = PenLineJoin.Round;
            p.StrokeStartLineCap = PenLineCap.Round;
            p.StrokeEndLineCap = PenLineCap.Round;
        }
        _plot.Children.Add(_fillB);
        _plot.Children.Add(_fillA);
        _plot.Children.Add(_lineB);
        _plot.Children.Add(_lineA);
        _plot.Children.Add(_dotB);
        _plot.Children.Add(_dotA);
        Children.Add(_grid);
        Children.Add(_plot);
        Children.Add(_maxLabel);
        _maxLabel.VerticalAlignment = VerticalAlignment.Top;
        _maxLabel.HorizontalAlignment = HorizontalAlignment.Left;
        Clip = _clip;
        ApplyBrushes();
        SizeChanged += (_, e) =>
        {
            _clip.Rect = new Rect(0, 0, e.NewSize.Width, e.NewSize.Height);
            DrawGrid();
            Redraw(animate: false, TimeSpan.Zero);
        };
    }

    private void ApplyBrushes()
    {
        _lineA.Stroke = new SolidColorBrush(_colorA);
        _lineB.Stroke = new SolidColorBrush(_colorB);
        _fillA.Fill = Fade(_colorA, 0x70);
        _fillB.Fill = Fade(_colorB, 0x50);
        _dotA.Fill = new SolidColorBrush(_colorA);
        _dotB.Fill = new SolidColorBrush(_colorB);
        _dotA.Stroke = _dotB.Stroke = new SolidColorBrush(Color.FromArgb(0xCC, 255, 255, 255));
        _dotA.StrokeThickness = _dotB.StrokeThickness = 1.5;
    }

    private static LinearGradientBrush Fade(Color c, byte alpha) => new()
    {
        StartPoint = new Point(0, 0),
        EndPoint = new Point(0, 1),
        GradientStops =
        {
            new GradientStop { Color = Color.FromArgb(alpha, c.R, c.G, c.B), Offset = 0 },
            new GradientStop { Color = Color.FromArgb(0, c.R, c.G, c.B), Offset = 1 },
        },
    };

    private void DrawGrid()
    {
        _grid.Children.Clear();
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        var stroke = new SolidColorBrush(Color.FromArgb(0x30, 0x80, 0x80, 0x80));
        for (int i = 1; i <= 3; i++)
        {
            double y = Math.Round(h * i / 4) + 0.5;
            _grid.Children.Add(new Line
            {
                X1 = 0, X2 = w, Y1 = y, Y2 = y,
                Stroke = stroke, StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 3, 4 },
            });
        }
    }

    public void Update(double[] a, double[]? b, TimeSpan duration)
    {
        _a = a;
        _b = b ?? [];
        Redraw(animate: duration > TimeSpan.Zero, duration);
    }

    private void Redraw(bool animate, TimeSpan duration)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0 || _a.Length == 0) return;

        double target = FixedMax;
        if (target <= 0)
        {
            double m = 0;
            foreach (var v in _a) m = Math.Max(m, v);
            foreach (var v in _b) m = Math.Max(m, v);
            target = NiceCeiling(Math.Max(m * 1.15, 1024));
            // ease the scale so the chart doesn't jump
            _displayMax = _displayMax <= 1 ? target : _displayMax + (target - _displayMax) * (target > _displayMax ? 1.0 : 0.35);
        }
        else _displayMax = target;

        _maxLabel.Text = FixedMax > 0 ? $"{FixedMax:0}%" : (LabelFormatter?.Invoke(_displayMax) ?? _displayMax.ToString("0"));

        double step = w / (Capacity - 1);
        double top = 6, usable = h - top - 2;
        double shift = animate ? step : 0;

        Build(_a, _fillA, _lineA, _dotA, w, h, step, top, usable, shift);
        if (_b.Length > 0) Build(_b, _fillB, _lineB, _dotB, w, h, step, top, usable, shift);
        else { _fillB.Data = null; _lineB.Data = null; _dotB.Visibility = Visibility.Collapsed; }

        _sb?.Stop();
        if (animate)
        {
            _shift.X = 0;
            var anim = new DoubleAnimation
            {
                From = 0,
                To = -step,
                Duration = new Duration(duration),
            };
            Storyboard.SetTarget(anim, _shift);
            Storyboard.SetTargetProperty(anim, "X");
            _sb = new Storyboard();
            _sb.Children.Add(anim);
            _sb.Begin();
        }
        else _shift.X = 0;
    }

    private void Build(double[] data, Path fill, Path line, Ellipse dot, double w, double h, double step, double top, double usable, double shift)
    {
        int n = data.Length;
        var pts = new Point[n];
        for (int i = 0; i < n; i++)
        {
            double x = w - (n - 1 - i) * step + shift;
            double v = Math.Clamp(data[i] / _displayMax, 0, 1);
            pts[i] = new Point(x, top + usable * (1 - v));
        }

        var lineFig = new PathFigure { StartPoint = pts[0], IsClosed = false };
        AddSmooth(lineFig.Segments, pts, top, top + usable);
        line.Data = new PathGeometry { Figures = { lineFig } };

        var fillFig = new PathFigure { StartPoint = new Point(pts[0].X, h), IsClosed = true, IsFilled = true };
        fillFig.Segments.Add(new LineSegment { Point = pts[0] });
        AddSmooth(fillFig.Segments, pts, top, top + usable);
        fillFig.Segments.Add(new LineSegment { Point = new Point(pts[^1].X, h) });
        fill.Data = new PathGeometry { Figures = { fillFig } };

        dot.Visibility = Visibility.Visible;
        Canvas.SetLeft(dot, pts[^1].X - dot.Width / 2);
        Canvas.SetTop(dot, pts[^1].Y - dot.Height / 2);
    }

    /// <summary>Catmull-Rom spline converted to cubic Béziers, with control points clamped to avoid overshoot.</summary>
    private static void AddSmooth(PathSegmentCollection segs, Point[] p, double minY, double maxY)
    {
        if (p.Length == 1) return;
        const double t = 0.18;
        for (int i = 0; i < p.Length - 1; i++)
        {
            Point p0 = p[Math.Max(i - 1, 0)], p1 = p[i], p2 = p[i + 1], p3 = p[Math.Min(i + 2, p.Length - 1)];
            var c1 = new Point(p1.X + (p2.X - p0.X) * t, Math.Clamp(p1.Y + (p2.Y - p0.Y) * t, minY, maxY));
            var c2 = new Point(p2.X - (p3.X - p1.X) * t, Math.Clamp(p2.Y - (p3.Y - p1.Y) * t, minY, maxY));
            segs.Add(new BezierSegment { Point1 = c1, Point2 = c2, Point3 = p2 });
        }
    }

    private static double NiceCeiling(double v)
    {
        double exp = Math.Pow(10, Math.Floor(Math.Log10(v)));
        double f = v / exp;
        double nice = f <= 1 ? 1 : f <= 2 ? 2 : f <= 2.5 ? 2.5 : f <= 5 ? 5 : 10;
        return nice * exp;
    }
}

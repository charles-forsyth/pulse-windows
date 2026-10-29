using Windows.Foundation;

namespace Pulse.Controls;

/// <summary>Responsive panel: lays children out in as many equal columns as fit MinItemWidth.
/// A child can span all columns with AdaptiveGrid.IsWide (when at least WideColumns are available).</summary>
public sealed partial class AdaptiveGrid : Panel
{
    public double MinItemWidth { get; set; } = 260;
    public double Spacing { get; set; } = 16;
    public int MaxColumns { get; set; } = 4;
    /// <summary>When true, column counts that would leave a ragged last row (e.g. 3 columns for 4 items) are reduced.</summary>
    public bool Balanced { get; set; }

    public static readonly DependencyProperty SpanProperty =
        DependencyProperty.RegisterAttached("Span", typeof(int), typeof(AdaptiveGrid), new PropertyMetadata(1));

    public static int GetSpan(DependencyObject o) => (int)o.GetValue(SpanProperty);
    public static void SetSpan(DependencyObject o, int v) => o.SetValue(SpanProperty, v);

    private int Columns(double width)
    {
        if (double.IsInfinity(width) || width <= 0) return 1;
        int cols = (int)Math.Floor((width + Spacing) / (MinItemWidth + Spacing));
        cols = Math.Clamp(cols, 1, MaxColumns);
        if (Balanced)
        {
            int n = Children.Count(c => c.Visibility == Visibility.Visible);
            while (cols > 1 && n > cols && n % cols != 0) cols--;
        }
        return cols;
    }

    private IEnumerable<(UIElement El, int Row, int Col, int Span)> Layout(int cols)
    {
        int row = 0, col = 0;
        foreach (var child in Children)
        {
            if (child.Visibility == Visibility.Collapsed) continue;
            int span = Math.Clamp(GetSpan(child), 1, cols);
            if (col + span > cols) { row++; col = 0; }
            yield return (child, row, col, span);
            col += span;
            if (col >= cols) { row++; col = 0; }
        }
    }

    private double[] _rowHeights = [];

    protected override Size MeasureOverride(Size available)
    {
        int cols = Columns(available.Width);
        double colW = double.IsInfinity(available.Width) ? MinItemWidth : (available.Width - Spacing * (cols - 1)) / cols;
        var heights = new List<double>();
        foreach (var (el, row, _, span) in Layout(cols))
        {
            double w = colW * span + Spacing * (span - 1);
            el.Measure(new Size(w, double.PositiveInfinity));
            while (heights.Count <= row) heights.Add(0);
            heights[row] = Math.Max(heights[row], el.DesiredSize.Height);
        }
        _rowHeights = heights.ToArray();
        double total = heights.Sum() + Spacing * Math.Max(0, heights.Count - 1);
        return new Size(double.IsInfinity(available.Width) ? colW * cols : available.Width, total);
    }

    protected override Size ArrangeOverride(Size final)
    {
        int cols = Columns(final.Width);
        double colW = (final.Width - Spacing * (cols - 1)) / cols;
        var yOf = new double[_rowHeights.Length + 1];
        for (int i = 0; i < _rowHeights.Length; i++) yOf[i + 1] = yOf[i] + _rowHeights[i] + Spacing;
        foreach (var (el, row, col, span) in Layout(cols))
        {
            double x = col * (colW + Spacing);
            double w = colW * span + Spacing * (span - 1);
            double h = row < _rowHeights.Length ? _rowHeights[row] : el.DesiredSize.Height;
            el.Arrange(new Rect(x, row < yOf.Length ? yOf[row] : 0, w, h));
        }
        return final;
    }
}

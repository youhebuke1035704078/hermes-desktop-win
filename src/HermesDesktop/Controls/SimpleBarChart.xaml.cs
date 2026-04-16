using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HermesDesktop.Controls;

public partial class SimpleBarChart : UserControl
{
    public static readonly DependencyProperty ChartTitleProperty =
        DependencyProperty.Register(nameof(ChartTitle), typeof(string), typeof(SimpleBarChart),
            new PropertyMetadata("", OnChartTitleChanged));

    public static readonly DependencyProperty ValuesProperty =
        DependencyProperty.Register(nameof(Values), typeof(IList<BarDataPoint>), typeof(SimpleBarChart),
            new PropertyMetadata(null, OnValuesChanged));

    public static readonly DependencyProperty MaxBarHeightProperty =
        DependencyProperty.Register(nameof(MaxBarHeight), typeof(double), typeof(SimpleBarChart),
            new PropertyMetadata(120.0, OnValuesChanged));

    public string ChartTitle
    {
        get => (string)GetValue(ChartTitleProperty);
        set => SetValue(ChartTitleProperty, value);
    }

    public IList<BarDataPoint>? Values
    {
        get => (IList<BarDataPoint>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public double MaxBarHeight
    {
        get => (double)GetValue(MaxBarHeightProperty);
        set => SetValue(MaxBarHeightProperty, value);
    }

    public SimpleBarChart()
    {
        InitializeComponent();
    }

    private static void OnChartTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SimpleBarChart chart)
            chart.TitleText.Text = e.NewValue as string ?? "";
    }

    private static void OnValuesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SimpleBarChart chart)
            chart.UpdateBars();
    }

    private void UpdateBars()
    {
        var values = Values;
        if (values == null || values.Count == 0)
        {
            BarsPanel.ItemsSource = null;
            return;
        }

        var maxVal = values.Max(v => v.Value);
        if (maxVal <= 0) maxVal = 1;

        var bars = new ObservableCollection<BarViewModel>();
        foreach (var point in values)
        {
            var ratio = point.Value / maxVal;
            var height = Math.Max(2, ratio * MaxBarHeight);

            // Color gradient from yellow (#FFC107) to red (#EF5350) based on ratio
            var r = (byte)(255);
            var g = (byte)(193 - (int)(143 * ratio));
            var b = (byte)(7 + (int)(73 * ratio));

            bars.Add(new BarViewModel
            {
                BarHeight = height,
                Fill = new SolidColorBrush(Color.FromRgb(r, g, b)),
                Tooltip = $"{point.Label}: {point.Value:N0} tokens"
            });
        }

        BarsPanel.ItemsSource = bars;
    }
}

public class BarDataPoint
{
    public string Label { get; set; } = "";
    public double Value { get; set; }
}

public class BarViewModel
{
    public double BarHeight { get; set; }
    public Brush Fill { get; set; } = Brushes.Orange;
    public string Tooltip { get; set; } = "";
}

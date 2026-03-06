using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using FlexComDotnet.Core.Features.Visualization.Models;
using FlexComDotnet.Core.Features.Visualization.ViewModels;
using Microsoft.Win32;
using ScottPlot;

namespace FlexComDotnet.Features.Visualization.Views;

/// <summary>
/// 数据可视化视图
/// </summary>
public partial class DataVisualizationView : UserControl
{
    private DataVisualizationViewModel? _viewModel;
    private readonly Dictionary<string, ScottPlot.Plottables.Scatter> _scatterPlots = [];
    private DateTime _startTime = DateTime.Now;
    private ScottPlot.Plottables.Marker? _inspectMarker;
    private ScottPlot.Plottables.VerticalLine? _inspectVLine;

    public DataVisualizationView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        InitializeChart();
    }

    public DataVisualizationView(DataVisualizationViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.ChartRefreshRequested -= OnChartRefreshRequested;
            _viewModel.ExportPngRequested -= OnExportPngRequested;
        }

        _viewModel = DataContext as DataVisualizationViewModel;

        if (_viewModel != null)
        {
            _viewModel.ChartRefreshRequested += OnChartRefreshRequested;
            _viewModel.ExportPngRequested += OnExportPngRequested;
        }
    }

    /// <summary>
    /// 初始化图表样式
    /// </summary>
    private void InitializeChart()
    {
        var plot = WpfPlot.Plot;

        // 深色主题
        plot.FigureBackground.Color = ScottPlot.Color.FromHex("#1E1E1E");
        plot.DataBackground.Color = ScottPlot.Color.FromHex("#252526");

        // 全局字体 - 自动检测支持中文的系统字体
        plot.Font.Set(ScottPlot.Fonts.Detect("时间数值"));

        // 坐标轴样式
        plot.Axes.Bottom.Label.Text = "时间 (s)";
        plot.Axes.Left.Label.Text = "数值";
        plot.Axes.Bottom.Label.ForeColor = ScottPlot.Color.FromHex("#CCCCCC");
        plot.Axes.Left.Label.ForeColor = ScottPlot.Color.FromHex("#CCCCCC");
        plot.Axes.Bottom.TickLabelStyle.ForeColor = ScottPlot.Color.FromHex("#999999");
        plot.Axes.Left.TickLabelStyle.ForeColor = ScottPlot.Color.FromHex("#999999");
        plot.Axes.Bottom.MajorTickStyle.Color = ScottPlot.Color.FromHex("#444444");
        plot.Axes.Left.MajorTickStyle.Color = ScottPlot.Color.FromHex("#444444");
        plot.Axes.Bottom.MinorTickStyle.Color = ScottPlot.Color.FromHex("#333333");
        plot.Axes.Left.MinorTickStyle.Color = ScottPlot.Color.FromHex("#333333");
        plot.Axes.Bottom.FrameLineStyle.Color = ScottPlot.Color.FromHex("#444444");
        plot.Axes.Left.FrameLineStyle.Color = ScottPlot.Color.FromHex("#444444");
        plot.Axes.Top.FrameLineStyle.Color = ScottPlot.Color.FromHex("#444444");
        plot.Axes.Right.FrameLineStyle.Color = ScottPlot.Color.FromHex("#444444");

        // 网格线
        plot.Grid.MajorLineColor = ScottPlot.Color.FromHex("#333333");
        plot.Grid.MinorLineColor = ScottPlot.Color.FromHex("#2A2A2A");

        // 图例
        plot.ShowLegend();
        plot.Legend.BackgroundColor = ScottPlot.Color.FromHex("#2D2D30");
        plot.Legend.FontColor = ScottPlot.Color.FromHex("#CCCCCC");
        plot.Legend.OutlineColor = ScottPlot.Color.FromHex("#444444");

        // 点击查看数据点
        WpfPlot.MouseLeftButtonUp += WpfPlot_MouseLeftButtonUp;
        WpfPlot.MouseRightButtonUp += WpfPlot_MouseRightButtonUp;

        WpfPlot.Refresh();
    }

    /// <summary>
    /// 左键点击图表 - 查看最近数据点
    /// </summary>
    private void WpfPlot_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel == null || _scatterPlots.Count == 0)
            return;

        try
        {
            // 使用 ScottPlot WPF 内置方法获取鼠标相对于图表绘图区域的像素坐标
            // 此方法内部已处理 DPI 缩放，返回正确的 Plot 像素坐标
            var mousePixel = WpfPlot.GetPlotPixelPosition(e);
            var mouseCoord = WpfPlot.Plot.GetCoordinates(mousePixel);

            // 获取当前可视范围，用于归一化距离
            var xRange = WpfPlot.Plot.Axes.Bottom.Range;
            var yRange = WpfPlot.Plot.Axes.Left.Range;
            var xSpan = xRange.Span > 0 ? xRange.Span : 1.0;
            var ySpan = yRange.Span > 0 ? yRange.Span : 1.0;

            // 在所有可见 scatter 中找最近的数据点
            string? bestChannelName = null;
            string? bestColor = null;
            double bestX = 0, bestY = 0;
            double bestDist = double.MaxValue;

            var allData = _viewModel.GetAllData();
            var channels = _viewModel.Channels;

            foreach (var channel in channels)
            {
                if (!channel.IsVisible)
                    continue;
                if (!allData.TryGetValue(channel.Id, out var dataPoints) || dataPoints.Count == 0)
                    continue;

                for (int i = 0; i < dataPoints.Count; i++)
                {
                    var dpX = (dataPoints[i].Timestamp - _startTime).TotalSeconds;
                    var dpY = dataPoints[i].Value;

                    // 归一化距离：将数据坐标差异映射到 [0,1] 范围
                    var dx = (dpX - mouseCoord.X) / xSpan;
                    var dy = (dpY - mouseCoord.Y) / ySpan;
                    var dist = dx * dx + dy * dy;

                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestX = dpX;
                        bestY = dpY;
                        bestChannelName = channel.DisplayName;
                        bestColor = channel.Color;
                    }
                }
            }

            // 归一化距离阈值: 0.01 约等于可视范围的 10%
            if (bestChannelName == null || bestDist > 0.01)
            {
                ClearInspectMarker();
                return;
            }

            ShowInspectMarker(bestX, bestY, bestChannelName, bestColor!);
        }
        catch
        {
            // Ignore coordinate conversion errors
        }
    }

    /// <summary>
    /// 右键点击图表 - 清除查看标记
    /// </summary>
    private void WpfPlot_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        ClearInspectMarker();
    }

    /// <summary>
    /// 显示数据点检查标记
    /// </summary>
    private void ShowInspectMarker(double x, double y, string channelName, string color)
    {
        var plot = WpfPlot.Plot;

        // 移除旧标记
        if (_inspectMarker != null)
            plot.Remove(_inspectMarker);
        if (_inspectVLine != null)
            plot.Remove(_inspectVLine);

        // 添加垂直线
        _inspectVLine = plot.Add.VerticalLine(x);
        _inspectVLine.Color = ScottPlot.Color.FromHex("#FFEB3B").WithAlpha(80);
        _inspectVLine.LineWidth = 1;
        _inspectVLine.LinePattern = ScottPlot.LinePattern.Dashed;

        // 添加标记点
        _inspectMarker = plot.Add.Marker(x, y);
        _inspectMarker.Color = ScottPlot.Color.FromHex(color);
        _inspectMarker.Size = 10;
        _inspectMarker.Shape = ScottPlot.MarkerShape.FilledCircle;

        // 更新提示框
        DataTipText.Text = $"{channelName}\nT: {x:F3}s\nV: {y:F4}";
        DataTipBorder.Visibility = Visibility.Visible;

        WpfPlot.Refresh();
    }

    /// <summary>
    /// 清除数据点检查标记
    /// </summary>
    private void ClearInspectMarker()
    {
        var plot = WpfPlot.Plot;

        if (_inspectMarker != null)
        {
            plot.Remove(_inspectMarker);
            _inspectMarker = null;
        }
        if (_inspectVLine != null)
        {
            plot.Remove(_inspectVLine);
            _inspectVLine = null;
        }

        DataTipBorder.Visibility = Visibility.Collapsed;
        WpfPlot.Refresh();
    }

    /// <summary>
    /// 响应图表刷新请求
    /// </summary>
    private void OnChartRefreshRequested(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(RefreshChart);
    }

    /// <summary>
    /// 刷新图表数据
    /// </summary>
    private void RefreshChart()
    {
        if (_viewModel == null)
            return;

        var allData = _viewModel.GetAllData();
        var channels = _viewModel.Channels;
        var plot = WpfPlot.Plot;

        // 清除已移除的通道
        var channelIds = new HashSet<string>(channels.Select(c => c.Id));
        var toRemove = _scatterPlots.Keys.Where(k => !channelIds.Contains(k)).ToList();
        foreach (var id in toRemove)
        {
            if (_scatterPlots.TryGetValue(id, out var scatter))
            {
                plot.Remove(scatter);
                _scatterPlots.Remove(id);
            }
        }

        foreach (var channel in channels)
        {
            if (!channel.IsVisible)
            {
                if (_scatterPlots.TryGetValue(channel.Id, out var hiddenPlot))
                {
                    hiddenPlot.IsVisible = false;
                }
                continue;
            }

            if (!allData.TryGetValue(channel.Id, out var dataPoints) || dataPoints.Count == 0)
                continue;

            var xData = dataPoints.Select(dp => (dp.Timestamp - _startTime).TotalSeconds).ToArray();
            var yData = dataPoints.Select(dp => dp.Value).ToArray();

            if (_scatterPlots.TryGetValue(channel.Id, out var existingPlot))
            {
                plot.Remove(existingPlot);
            }

            var scatter = plot.Add.ScatterLine(xData, yData);
            scatter.LegendText = channel.DisplayName;
            scatter.Color = ScottPlot.Color.FromHex(channel.Color);
            scatter.LineWidth = channel.LineWidth;
            scatter.IsVisible = channel.IsVisible;
            _scatterPlots[channel.Id] = scatter;
        }

        plot.Axes.AutoScale();

        // 当所有 Y 值相同时（平坦线），AutoScale 产生零范围，ScottPlot 无法渲染
        // 添加 padding 确保线条可见
        var yRange = plot.Axes.Left.Range;
        if (yRange.Span == 0)
        {
            var center = yRange.Min;
            var padding = Math.Max(Math.Abs(center) * 0.1, 1.0);
            plot.Axes.SetLimitsY(center - padding, center + padding);
        }

        WpfPlot.Refresh();
    }

    /// <summary>
    /// 导出 PNG 图片
    /// </summary>
    private void OnExportPngRequested(object? sender, string filePath)
    {
        Dispatcher.Invoke(() =>
        {
            try
            {
                WpfPlot.Plot.SavePng(filePath, (int)WpfPlot.ActualWidth, (int)WpfPlot.ActualHeight);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });
    }

    /// <summary>
    /// 导出 CSV 按钮点击
    /// </summary>
    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "CSV 文件 (*.csv)|*.csv",
            DefaultExt = ".csv",
            FileName = $"visualization_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (dialog.ShowDialog() == true)
        {
            _viewModel?.ExportCsvCommand.Execute(dialog.FileName);
        }
    }

    /// <summary>
    /// 导出 PNG 按钮点击
    /// </summary>
    private void ExportPng_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "PNG 图片 (*.png)|*.png",
            DefaultExt = ".png",
            FileName = $"visualization_{DateTime.Now:yyyyMMdd_HHmmss}.png"
        };

        if (dialog.ShowDialog() == true)
        {
            _viewModel?.ExportPngCommand.Execute(dialog.FileName);
        }
    }

    /// <summary>
    /// 通道标签点击 - 选中通道
    /// </summary>
    private void ChannelTag_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is ChannelConfig channel)
        {
            if (_viewModel != null)
            {
                _viewModel.SelectedChannel = channel;
            }
        }
    }
}

/// <summary>
/// 颜色字符串转 WPF Color 转换器
/// </summary>
public class ColorStringConverter : IValueConverter
{
    public static readonly ColorStringConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string colorStr && !string.IsNullOrEmpty(colorStr))
        {
            try
            {
                return (System.Windows.Media.Color)ColorConverter.ConvertFromString(colorStr);
            }
            catch
            {
                return System.Windows.Media.Colors.Gray;
            }
        }
        return System.Windows.Media.Colors.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Bool 到透明度转换器
/// </summary>
public class BoolToOpacityConverter : IValueConverter
{
    public static readonly BoolToOpacityConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && b ? 1.0 : 0.3;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

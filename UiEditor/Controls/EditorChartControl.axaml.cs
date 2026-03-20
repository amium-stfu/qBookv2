using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ScottPlot;
using ScottPlot.Avalonia;
using UiEditor.Host;
using UiEditor.Items;
using UiEditor.Models;
using UiEditor.ViewModels;

namespace UiEditor.Controls;

public partial class EditorChartControl : UserControl
{
    private static readonly ScottPlot.Color[] SeriesColors =
    [
        Colors.DodgerBlue,
        Colors.Orange,
        Colors.LimeGreen,
        Colors.DeepPink,
        Colors.Gold,
        Colors.Cyan,
        Colors.Violet,
        Colors.Tomato
    ];

    private readonly object _seriesLock = new();
    private readonly Dictionary<string, List<ChartPoint>> _seriesPoints = new(StringComparer.Ordinal);
    private DispatcherTimer? _renderTimer;
    private PageItemModel? _chartItem;
    private AvaPlot? _avaPlot;
    private Grid? _plotHost;
    private Canvas? _crosshairOverlay;
    private Border? _crosshairVerticalLine;
    private Border? _crosshairHorizontalLine;
    private Border? _crosshairInfoBorder;
    private TextBlock? _crosshairInfoTextBlock;
    private IYAxis? _yAxis2;
    private IYAxis? _yAxis3;
    private IYAxis? _yAxis4;
    private Button? _pauseButton;
    private TextBlock? _statusTextBlock;
    private bool _isPaused;
    private bool _isDirty;
    private bool _hasConfiguredAxes;

    public EditorChartControl()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
        DataContextChanged += OnDataContextChanged;
    }

    private MainWindowViewModel? ViewModel
        => this.GetVisualRoot() is Window { DataContext: MainWindowViewModel viewModel } ? viewModel : null;

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _avaPlot = this.FindControl<AvaPlot>("ChartPlot");
        _plotHost = this.FindControl<Grid>("PlotHost");
        _crosshairOverlay = this.FindControl<Canvas>("CrosshairOverlay");
        _crosshairVerticalLine = this.FindControl<Border>("CrosshairVerticalLine");
        _crosshairHorizontalLine = this.FindControl<Border>("CrosshairHorizontalLine");
        _crosshairInfoBorder = this.FindControl<Border>("CrosshairInfoBorder");
        _crosshairInfoTextBlock = this.FindControl<TextBlock>("CrosshairInfoTextBlock");
        _pauseButton = this.FindControl<Button>("PauseButton");
        _statusTextBlock = this.FindControl<TextBlock>("StatusTextBlock");
        ConfigurePlot();
        HookChartItem(DataContext as PageItemModel);
        StartRenderTimer();
        RenderPlot();
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        StopRenderTimer();
        HookChartItem(null);
        _avaPlot = null;
        _plotHost = null;
        _crosshairOverlay = null;
        _crosshairVerticalLine = null;
        _crosshairHorizontalLine = null;
        _crosshairInfoBorder = null;
        _crosshairInfoTextBlock = null;
        _yAxis2 = null;
        _yAxis3 = null;
        _yAxis4 = null;
        _hasConfiguredAxes = false;
        _pauseButton = null;
        _statusTextBlock = null;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        HookChartItem(DataContext as PageItemModel);
        RenderPlot();
    }

    private void HookChartItem(PageItemModel? nextItem)
    {
        if (ReferenceEquals(_chartItem, nextItem))
        {
            return;
        }

        if (_chartItem is not null)
        {
            _chartItem.PropertyChanged -= OnChartItemPropertyChanged;
        }

        HostRegistries.Data.ItemChanged -= OnRegistryItemChanged;
        _chartItem = nextItem;

        if (_chartItem is not null)
        {
            _chartItem.PropertyChanged += OnChartItemPropertyChanged;
        }

        ClearSeriesInternal();
        SeedCurrentValues();
        HostRegistries.Data.ItemChanged += OnRegistryItemChanged;
        UpdateStatusText();
    }

    private void OnChartItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PageItemModel.TargetPath)
            or nameof(PageItemModel.ChartSeriesDefinitions)
            or nameof(PageItemModel.HistorySeconds)
            or nameof(PageItemModel.ViewSeconds))
        {
            ClearSeriesInternal();
            SeedCurrentValues();
            TrimSeries();
            UpdateStatusText();
            HideCrosshair();
            RenderPlot();
        }

        if (e.PropertyName is nameof(PageItemModel.RefreshRateMs))
        {
            StartRenderTimer();
        }

        if (e.PropertyName is nameof(PageItemModel.EffectiveBackground)
            or nameof(PageItemModel.EffectiveContainerBackground)
            or nameof(PageItemModel.EffectivePrimaryForeground)
            or nameof(PageItemModel.EffectiveSecondaryForeground)
            or nameof(PageItemModel.EffectiveContainerBorderBrush)
            or nameof(PageItemModel.Title)
            or nameof(PageItemModel.Footer))
        {
            UpdateStatusText();
            RenderPlot();
        }
    }

    private void OnRegistryItemChanged(object? sender, DataChangedEventArgs e)
    {
        var seriesConfigs = GetSeriesConfigurations();
        if (seriesConfigs.Count == 0)
        {
            return;
        }

        var matchingConfigs = seriesConfigs.Where(config => string.Equals(config.TargetPath, e.Key, StringComparison.Ordinal)).ToList();
        if (matchingConfigs.Count == 0)
        {
            return;
        }

        if (!TryResolveNumericValue(e.Item, out var value))
        {
            return;
        }

        var timestamp = DateTimeOffset.FromUnixTimeMilliseconds((long)e.Timestamp).LocalDateTime;
        lock (_seriesLock)
        {
            foreach (var config in matchingConfigs)
            {
                if (!_seriesPoints.TryGetValue(config.Key, out var points))
                {
                    points = [];
                    _seriesPoints[config.Key] = points;
                }

                points.Add(new ChartPoint(timestamp, value));
            }
        }

        TrimSeries();
        MarkDirty();
    }

    private void StartRenderTimer()
    {
        StopRenderTimer();

        var interval = _chartItem?.RefreshRateMs ?? 1000;
        _renderTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(Math.Max(30, interval <= 0 ? 30 : interval))
        };
        _renderTimer.Tick += OnRenderTimerTick;
        _renderTimer.Start();
    }

    private void StopRenderTimer()
    {
        if (_renderTimer is null)
        {
            return;
        }

        _renderTimer.Stop();
        _renderTimer.Tick -= OnRenderTimerTick;
        _renderTimer = null;
    }

    private void OnRenderTimerTick(object? sender, EventArgs e)
    {
        if (_isPaused || !_isDirty)
        {
            return;
        }

        RenderPlot();
    }

    private void ConfigurePlot()
    {
        if (_avaPlot is null)
        {
            return;
        }

        _avaPlot.Plot.ShowLegend(Alignment.UpperLeft);
        EnsureAxesCreated();
        ApplyPlotTheme();
        UpdateInteractionState();
    }

    private void EnsureAxesCreated()
    {
        if (_avaPlot is null)
        {
            return;
        }

        var plot = _avaPlot.Plot;
        if (!_hasConfiguredAxes)
        {
            plot.Axes.DateTimeTicksBottom();
            _hasConfiguredAxes = true;
        }

        plot.Axes.Left.Label.Text = "Y1";
        plot.Axes.Left.IsVisible = true;
        if (_yAxis2 is not null) _yAxis2.IsVisible = false;
        if (_yAxis3 is not null) _yAxis3.IsVisible = false;
        if (_yAxis4 is not null) _yAxis4.IsVisible = false;

        _yAxis2 ??= plot.Axes.AddLeftAxis();
        _yAxis3 ??= plot.Axes.AddLeftAxis();
        _yAxis4 ??= plot.Axes.AddLeftAxis();

        _yAxis2.Label.Text = "Y2";
        _yAxis3.Label.Text = "Y3";
        _yAxis4.Label.Text = "Y4";
    }

    private void ApplyPlotTheme()
    {
        if (_avaPlot is null)
        {
            return;
        }

        var plot = _avaPlot.Plot;
        var darkMode = ViewModel?.IsDarkTheme ?? IsDarkColor(_chartItem?.EffectiveBackground);
        var figureBackground = ParseScottPlotColor(_chartItem?.EffectiveContainerBackground) ?? (darkMode ? Colors.Black : Colors.White);
        var axesColor = ParseScottPlotColor(_chartItem?.EffectivePrimaryForeground) ?? (darkMode ? Colors.White : Colors.Black);
        var gridColor = ParseScottPlotColor(ViewModel?.GridLineBrush) ?? (darkMode ? Colors.DimGray : Colors.LightGray);
        var legendBackground = ParseScottPlotColor(_chartItem?.EffectiveBackground) ?? figureBackground;

        plot.FigureBackground.Color = figureBackground;
        plot.DataBackground.Color = figureBackground;
        plot.Grid.MajorLineColor = gridColor;
        plot.Grid.MinorLineColor = gridColor;
        plot.Axes.Color(axesColor);
        plot.Legend.BackgroundColor = legendBackground;
        plot.Legend.FontColor = axesColor;
        plot.Legend.Alignment = Alignment.UpperLeft;
    }

    private void UpdateInteractionState()
    {
        if (_avaPlot?.UserInputProcessor is { } userInputProcessor)
        {
            userInputProcessor.Disable();
        }

        if (!_isPaused)
        {
            HideCrosshair();
        }
    }

    private void RenderPlot()
    {
        if (_avaPlot is null)
        {
            return;
        }

        var seriesConfigs = GetSeriesConfigurations();
        lock (_seriesLock)
        {
            TrimSeriesLocked();
        }

        var plot = _avaPlot.Plot;
        plot.Clear();
        EnsureAxesCreated();
        ApplyPlotTheme();

        var axisMap = CreateAxisMap(plot);
        var latestTimestamp = DateTime.MinValue;
        var hasData = false;

        for (var i = 0; i < seriesConfigs.Count; i++)
        {
            var config = seriesConfigs[i];
            ChartPoint[] points;
            lock (_seriesLock)
            {
                points = _seriesPoints.TryGetValue(config.Key, out var list) ? list.ToArray() : [];
            }

            if (points.Length == 0)
            {
                continue;
            }

            hasData = true;
            latestTimestamp = latestTimestamp < points[^1].Timestamp ? points[^1].Timestamp : latestTimestamp;

            var xs = points.Select(point => point.Timestamp.ToOADate()).ToArray();
            var ys = points.Select(point => point.Value).ToArray();
            var scatter = plot.Add.Scatter(xs, ys);
            scatter.LegendText = config.DisplayName;
            scatter.LineWidth = 2;
            scatter.MarkerSize = 0;
            scatter.Color = SeriesColors[i % SeriesColors.Length];
            scatter.Axes.YAxis = axisMap[config.AxisIndex];
        }

        if (hasData)
        {
            var viewSeconds = Math.Max(1, _chartItem?.ViewSeconds ?? 30);
            plot.Axes.SetLimitsX(latestTimestamp.AddSeconds(-viewSeconds).ToOADate(), latestTimestamp.ToOADate());
            foreach (var axisIndex in seriesConfigs.Select(config => config.AxisIndex).Distinct().OrderBy(index => index))
            {
                if (axisMap.TryGetValue(axisIndex, out var axis))
                {
                    plot.Axes.AutoScaleY(axis);
                }
            }
        }

        _avaPlot.Refresh();
        _isDirty = false;
        UpdateStatusText();
    }

    private Dictionary<int, IYAxis> CreateAxisMap(Plot plot)
    {
        var axisMap = new Dictionary<int, IYAxis>
        {
            [1] = plot.Axes.Left
        };

        plot.Axes.Left.Label.Text = "Y1";
        plot.Axes.Left.IsVisible = true;
        if (_yAxis2 is not null)
        {
            axisMap[2] = _yAxis2;
            _yAxis2.IsVisible = true;
        }
        if (_yAxis3 is not null)
        {
            axisMap[3] = _yAxis3;
            _yAxis3.IsVisible = true;
        }
        if (_yAxis4 is not null)
        {
            axisMap[4] = _yAxis4;
            _yAxis4.IsVisible = true;
        }

        return axisMap;
    }

    private void SeedCurrentValues()
    {
        foreach (var config in GetSeriesConfigurations())
        {
            if (!HostRegistries.Data.TryGet(config.TargetPath, out var item) || item is null)
            {
                continue;
            }

            if (!TryResolveNumericValue(item, out var value))
            {
                continue;
            }

            var timestamp = item.Params.Has("Value")
                ? DateTimeOffset.FromUnixTimeMilliseconds((long)item.Params["Value"].LastUpdate).LocalDateTime
                : DateTime.Now;

            lock (_seriesLock)
            {
                if (!_seriesPoints.TryGetValue(config.Key, out var points))
                {
                    points = [];
                    _seriesPoints[config.Key] = points;
                }

                points.Add(new ChartPoint(timestamp, value));
            }
        }

        TrimSeries();
        MarkDirty();
    }

    private void TrimSeries()
    {
        lock (_seriesLock)
        {
            TrimSeriesLocked();
        }
    }

    private void TrimSeriesLocked()
    {
        var historySeconds = Math.Max(1, _chartItem?.HistorySeconds ?? 120);
        foreach (var series in _seriesPoints.Values)
        {
            if (series.Count == 0)
            {
                continue;
            }

            var cutoff = series[^1].Timestamp.AddSeconds(-historySeconds);
            series.RemoveAll(point => point.Timestamp < cutoff);
        }
    }

    private void ClearSeriesInternal()
    {
        lock (_seriesLock)
        {
            _seriesPoints.Clear();
        }
    }

    private void MarkDirty()
    {
        _isDirty = true;
    }

    private List<ChartSeriesConfiguration> GetSeriesConfigurations()
    {
        var item = _chartItem;
        if (item is null)
        {
            return [];
        }

        var parsed = ParseSeriesDefinitions(item.ChartSeriesDefinitions);
        if (parsed.Count > 0)
        {
            return parsed;
        }

        if (!string.IsNullOrWhiteSpace(item.TargetPath))
        {
            return [CreateSeriesConfiguration(item.TargetPath, 1)];
        }

        return [];
    }

    private List<ChartSeriesConfiguration> ParseSeriesDefinitions(string? raw)
    {
        var result = new List<ChartSeriesConfiguration>();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return result;
        }

        var lines = raw.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in lines)
        {
            var parts = line.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || string.IsNullOrWhiteSpace(parts[0]))
            {
                continue;
            }

            var axisIndex = 1;
            if (parts.Length > 1)
            {
                var axisText = parts[1].StartsWith("Y", StringComparison.OrdinalIgnoreCase) ? parts[1][1..] : parts[1];
                if (!int.TryParse(axisText, NumberStyles.Integer, CultureInfo.InvariantCulture, out axisIndex))
                {
                    axisIndex = 1;
                }
            }

            result.Add(CreateSeriesConfiguration(parts[0], axisIndex));
        }

        return result;
    }

    private static ChartSeriesConfiguration CreateSeriesConfiguration(string targetPath, int axisIndex)
    {
        var normalizedAxis = Math.Clamp(axisIndex, 1, 4);
        var displayName = targetPath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? targetPath;
        return new ChartSeriesConfiguration(targetPath, normalizedAxis, $"{targetPath}|Y{normalizedAxis}", $"Y{normalizedAxis} {displayName}");
    }

    private void UpdateStatusText()
    {
        if (_pauseButton is not null)
        {
            _pauseButton.Content = _isPaused ? "Live" : "Pause";
        }

        if (_statusTextBlock is null)
        {
            return;
        }

        var seriesCount = GetSeriesConfigurations().Count;
        if (seriesCount == 0)
        {
            _statusTextBlock.Text = "Keine Serien konfiguriert";
            return;
        }

        var modeText = _isPaused ? "Pause | Maus zeigt Fadenkreuz und Messwerte" : "Live | Zoom und Pan gesperrt";
        _statusTextBlock.Text = $"{seriesCount} Serien | X=DateTime | {modeText} | History {_chartItem?.HistorySeconds ?? 120}s | View {_chartItem?.ViewSeconds ?? 30}s";
    }

    private void OnPlotPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPaused || _plotHost is null)
        {
            HideCrosshair();
            return;
        }

        var position = e.GetPosition(_plotHost);
        if (position.X < 0 || position.Y < 0 || position.X > _plotHost.Bounds.Width || position.Y > _plotHost.Bounds.Height)
        {
            HideCrosshair();
            return;
        }

        UpdateCrosshair(position);
    }

    private void OnPlotPointerExited(object? sender, PointerEventArgs e)
    {
        HideCrosshair();
    }

    private void UpdateCrosshair(Point position)
    {
        if (_plotHost is null || _crosshairOverlay is null || _crosshairVerticalLine is null || _crosshairHorizontalLine is null)
        {
            return;
        }

        var width = Math.Max(0, _plotHost.Bounds.Width);
        var height = Math.Max(0, _plotHost.Bounds.Height);

        _crosshairOverlay.Width = width;
        _crosshairOverlay.Height = height;

        _crosshairVerticalLine.Height = height;
        Canvas.SetLeft(_crosshairVerticalLine, position.X);
        Canvas.SetTop(_crosshairVerticalLine, 0);
        _crosshairVerticalLine.IsVisible = true;

        _crosshairHorizontalLine.Width = width;
        Canvas.SetLeft(_crosshairHorizontalLine, 0);
        Canvas.SetTop(_crosshairHorizontalLine, position.Y);
        _crosshairHorizontalLine.IsVisible = true;

        if (_crosshairInfoBorder is null || _crosshairInfoTextBlock is null)
        {
            return;
        }

        if (TryBuildCrosshairText(position, out var text))
        {
            _crosshairInfoTextBlock.Text = text;
            _crosshairInfoBorder.IsVisible = true;
        }
        else
        {
            _crosshairInfoTextBlock.Text = string.Empty;
            _crosshairInfoBorder.IsVisible = false;
        }
    }

    private bool TryBuildCrosshairText(Point position, out string text)
    {
        text = string.Empty;
        if (_avaPlot is null)
        {
            return false;
        }

        var plot = _avaPlot.Plot;
        var coordinates = plot.GetCoordinates((float)position.X, (float)position.Y, plot.Axes.Bottom, plot.Axes.Left);
        if (double.IsNaN(coordinates.X) || double.IsInfinity(coordinates.X))
        {
            return false;
        }

        DateTime cursorTime;
        try
        {
            cursorTime = DateTime.FromOADate(coordinates.X);
        }
        catch
        {
            return false;
        }

        var lines = new List<string>
        {
            cursorTime.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture)
        };

        foreach (var config in GetSeriesConfigurations())
        {
            if (TryGetNearestPoint(config.Key, coordinates.X, out var point))
            {
               // lines.Add($"{config.DisplayName}: {FormatValue(point.Value)} ({point.Timestamp:HH:mm:ss})");
                lines.Add($"{config.DisplayName}: {FormatValue(point.Value)}");
            }
            else
            {
                lines.Add($"{config.DisplayName}: n/a");
            }
        }

        text = string.Join(Environment.NewLine, lines);
        return true;
    }

    private bool TryGetNearestPoint(string key, double xPosition, out ChartPoint point)
    {
        lock (_seriesLock)
        {
            if (!_seriesPoints.TryGetValue(key, out var points) || points.Count == 0)
            {
                point = default;
                return false;
            }

            var low = 0;
            var high = points.Count - 1;
            while (low < high)
            {
                var mid = (low + high) / 2;
                if (points[mid].Timestamp.ToOADate() < xPosition)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid;
                }
            }

            var upperIndex = low;
            var lowerIndex = Math.Max(0, upperIndex - 1);
            var upperDistance = Math.Abs(points[upperIndex].Timestamp.ToOADate() - xPosition);
            var lowerDistance = Math.Abs(points[lowerIndex].Timestamp.ToOADate() - xPosition);
            point = lowerDistance <= upperDistance ? points[lowerIndex] : points[upperIndex];
            return true;
        }
    }

    private void HideCrosshair()
    {
        if (_crosshairVerticalLine is not null)
        {
            _crosshairVerticalLine.IsVisible = false;
        }

        if (_crosshairHorizontalLine is not null)
        {
            _crosshairHorizontalLine.IsVisible = false;
        }

        if (_crosshairInfoBorder is not null)
        {
            _crosshairInfoBorder.IsVisible = false;
        }

        if (_crosshairInfoTextBlock is not null)
        {
            _crosshairInfoTextBlock.Text = string.Empty;
        }
    }

    private static string FormatValue(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static bool TryResolveNumericValue(Item item, out double value)
    {
        value = 0;
        var rawValue = item.Value;
        if (rawValue is null)
        {
            return false;
        }

        try
        {
            value = Convert.ToDouble(rawValue, CultureInfo.InvariantCulture);
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsDarkColor(string? colorText)
    {
        if (string.IsNullOrWhiteSpace(colorText))
        {
            return false;
        }

        if (!Avalonia.Media.Color.TryParse(colorText, out var color))
        {
            return false;
        }

        var brightness = (0.299 * color.R) + (0.587 * color.G) + (0.114 * color.B);
        return brightness < 140;
    }

    private static ScottPlot.Color? ParseScottPlotColor(string? colorText)
    {
        if (string.IsNullOrWhiteSpace(colorText) || !Avalonia.Media.Color.TryParse(colorText, out var color))
        {
            return null;
        }

        return new ScottPlot.Color(color.R, color.G, color.B, color.A);
    }

    private void OnSettingsClicked(object? sender, RoutedEventArgs e)
    {
        if (_chartItem is null || ViewModel is null || this.GetVisualAncestors().OfType<PageEditorControl>().FirstOrDefault() is not { } editor)
        {
            return;
        }

        var anchor = this.TranslatePoint(new Point(Bounds.Width + 8, 0), editor) ?? new Point(24, 24);
        ViewModel.OpenItemEditor(_chartItem, anchor.X, anchor.Y);
        e.Handled = true;
    }

    private void OnInteractivePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
    }

    private void OnPauseClicked(object? sender, RoutedEventArgs e)
    {
        _isPaused = !_isPaused;
        UpdateInteractionState();
        UpdateStatusText();
        if (!_isPaused)
        {
            RenderPlot();
        }
    }

    private void OnClearClicked(object? sender, RoutedEventArgs e)
    {
        ClearSeriesInternal();
        SeedCurrentValues();
        HideCrosshair();
        RenderPlot();
    }

    private readonly record struct ChartPoint(DateTime Timestamp, double Value);

    private sealed record ChartSeriesConfiguration(string TargetPath, int AxisIndex, string Key, string DisplayName);
}





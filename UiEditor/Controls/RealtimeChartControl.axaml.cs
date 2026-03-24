using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Amium.EditorUi.Controls;
using Amium.Host;
using Amium.Items;
using Amium.UiEditor.Models;
using Amium.UiEditor.ViewModels;
using ScottPlot;
using ScottPlot.Avalonia;

namespace Amium.UiEditor.Controls;

public partial class RealtimeChartControl : EditorTemplateControl
{
    public static readonly StyledProperty<bool> PageIsActiveProperty =
        AvaloniaProperty.Register<RealtimeChartControl, bool>(nameof(PageIsActive), true);

    public static readonly StyledProperty<bool> IsPausedProperty =
        AvaloniaProperty.Register<RealtimeChartControl, bool>(nameof(IsPaused));

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

    private static readonly object ChartStatesLock = new();
    private static readonly Dictionary<string, ChartRuntimeState> ChartStates = new(StringComparer.Ordinal);

    private DispatcherTimer? _renderTimer;
    private PageItemModel? _chartItem;
    private ChartRuntimeState? _chartState;
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
    private bool _hasConfiguredAxes;

    private MainWindowViewModel? ViewModel
        => this.GetVisualRoot() is Window { DataContext: MainWindowViewModel viewModel } ? viewModel : null;

    public bool PageIsActive
    {
        get => GetValue(PageIsActiveProperty);
        set => SetValue(PageIsActiveProperty, value);
    }

    public bool IsPaused
    {
        get => GetValue(IsPausedProperty);
        private set => SetValue(IsPausedProperty, value);
    }

    public RealtimeChartControl()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
        DataContextChanged += OnDataContextChanged;
    }

    private object? PlotSyncRoot => _avaPlot?.Plot.Sync;

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _avaPlot = this.FindControl<AvaPlot>("ChartPlot");
        _plotHost = this.FindControl<Grid>("PlotHost");
        _crosshairOverlay = this.FindControl<Canvas>("CrosshairOverlay");
        _crosshairVerticalLine = this.FindControl<Border>("CrosshairVerticalLine");
        _crosshairHorizontalLine = this.FindControl<Border>("CrosshairHorizontalLine");
        _crosshairInfoBorder = this.FindControl<Border>("CrosshairInfoBorder");
        _crosshairInfoTextBlock = this.FindControl<TextBlock>("CrosshairInfoTextBlock");

        ConfigurePlot();
        HookChartItem(DataContext as PageItemModel);
        UpdateRenderActivity();
        if (PageIsActive && IsVisible)
        {
            RenderPlot();
        }
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
        _chartState = null;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        HookChartItem(DataContext as PageItemModel);
        if (PageIsActive && IsVisible)
        {
            RenderPlot();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == PageIsActiveProperty || change.Property == IsVisibleProperty)
        {
            UpdateRenderActivity();
            if (PageIsActive && IsVisible)
            {
                RenderPlot();
            }
            else
            {
                HideCrosshair();
            }
        }

        if (change.Property == IsPausedProperty)
        {
            UpdateInteractionState();
            UpdateStatusText();
            if (!IsPaused && PageIsActive && IsVisible)
            {
                RenderPlot();
            }
        }
    }

    private void HookChartItem(PageItemModel? nextItem)
    {
        if (ReferenceEquals(_chartItem, nextItem))
        {
            if (_chartItem is not null)
            {
                _chartState = GetOrCreateChartState(_chartItem);
                _chartState.UpdateConfiguration(CreateChartStateConfiguration(_chartItem));
            }

            return;
        }

        if (_chartItem is not null)
        {
            _chartItem.PropertyChanged -= OnChartItemPropertyChanged;
        }

        _chartItem = nextItem;
        _chartState = null;

        if (_chartItem is not null)
        {
            _chartItem.PropertyChanged += OnChartItemPropertyChanged;
            _chartState = GetOrCreateChartState(_chartItem);
            _chartState.UpdateConfiguration(CreateChartStateConfiguration(_chartItem));
        }

        UpdateStatusText();
    }

    private void OnChartItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PageItemModel.TargetPath)
            or nameof(PageItemModel.ChartSeriesDefinitions)
            or nameof(PageItemModel.HistorySeconds)
            or nameof(PageItemModel.ViewSeconds)
            or nameof(PageItemModel.Id))
        {
            if (_chartItem is not null)
            {
                _chartState = GetOrCreateChartState(_chartItem);
                _chartState.UpdateConfiguration(CreateChartStateConfiguration(_chartItem));
            }

            UpdateStatusText();
            HideCrosshair();
            if (PageIsActive && IsVisible)
            {
                RenderPlot();
            }
        }

        if (e.PropertyName is nameof(PageItemModel.RefreshRateMs))
        {
            if (_chartItem is not null)
            {
                _chartState = GetOrCreateChartState(_chartItem);
                _chartState.UpdateConfiguration(CreateChartStateConfiguration(_chartItem));
            }

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
            if (PageIsActive && IsVisible)
            {
                RenderPlot();
            }
        }
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

    private void UpdateRenderActivity()
    {
        if (PageIsActive && IsVisible)
        {
            StartRenderTimer();
            return;
        }

        StopRenderTimer();
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
        if (IsPaused)
        {
            return;
        }

        RenderPlot();
    }

    private void ConfigurePlot()
    {
        if (_avaPlot is null || PlotSyncRoot is not { } syncRoot)
        {
            return;
        }

        lock (syncRoot)
        {
            _avaPlot.Plot.ShowLegend(Alignment.UpperLeft);
            EnsureAxesCreated();
            ApplyPlotTheme();
            UpdateInteractionState();
        }
    }

    private void EnsureAxesCreated()
    {
        if (_avaPlot is null || PlotSyncRoot is not { } syncRoot)
        {
            return;
        }

        lock (syncRoot)
        {
            var plot = _avaPlot.Plot;
            if (!_hasConfiguredAxes)
            {
                plot.Axes.DateTimeTicksBottom();
                _hasConfiguredAxes = true;
            }

            plot.Axes.Left.Label.Text = "Y1";
            plot.Axes.Left.IsVisible = true;

            _yAxis2 ??= plot.Axes.AddLeftAxis();
            _yAxis3 ??= plot.Axes.AddLeftAxis();
            _yAxis4 ??= plot.Axes.AddLeftAxis();

            _yAxis2.Label.Text = "Y2";
            _yAxis3.Label.Text = "Y3";
            _yAxis4.Label.Text = "Y4";
            _yAxis2.IsVisible = false;
            _yAxis3.IsVisible = false;
            _yAxis4.IsVisible = false;
        }
    }

    private void ApplyPlotTheme()
    {
        if (_avaPlot is null || PlotSyncRoot is not { } syncRoot)
        {
            return;
        }

        lock (syncRoot)
        {
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
    }

    private void UpdateInteractionState()
    {
        if (_avaPlot?.UserInputProcessor is { } userInputProcessor && PlotSyncRoot is { } syncRoot)
        {
            lock (syncRoot)
            {
                if (IsPaused)
                {
                    userInputProcessor.Enable();
                }
                else
                {
                    userInputProcessor.Disable();
                }
            }
        }

        if (!IsPaused)
        {
            HideCrosshair();
        }
    }

    private void RenderPlot()
    {
        if (!PageIsActive || !IsVisible || _avaPlot is null || PlotSyncRoot is not { } syncRoot)
        {
            return;
        }

        var seriesSnapshots = _chartState?.GetSeriesSnapshots() ?? [];
        var activeAxisIndexes = seriesSnapshots
            .Where(snapshot => snapshot.Points.Length > 0)
            .Select(snapshot => snapshot.Configuration.AxisIndex)
            .Distinct()
            .OrderBy(index => index)
            .ToList();

        lock (syncRoot)
        {
            var plot = _avaPlot.Plot;
            plot.Clear();
            EnsureAxesCreated();
            ApplyPlotTheme();

            var axisMap = CreateAxisMap(plot, activeAxisIndexes);
            var now = DateTime.Now;
            var hasData = false;

            for (var i = 0; i < seriesSnapshots.Count; i++)
            {
                var snapshot = seriesSnapshots[i];
                if (snapshot.Points.Length == 0)
                {
                    continue;
                }

                hasData = true;
                var xs = snapshot.Points.Select(point => point.Timestamp.ToOADate()).ToArray();
                var ys = snapshot.Points.Select(point => point.Value).ToArray();
                var scatter = plot.Add.Scatter(xs, ys);
                scatter.LegendText = snapshot.Configuration.DisplayName;
                scatter.LineWidth = 2;
                scatter.MarkerSize = 0;
                scatter.Color = SeriesColors[i % SeriesColors.Length];
                scatter.ConnectStyle = snapshot.Configuration.ConnectStyle;
                scatter.Axes.YAxis = axisMap[snapshot.Configuration.AxisIndex];
            }

            var viewSeconds = Math.Max(1, _chartItem?.ViewSeconds ?? 30);
            plot.Axes.SetLimitsX(now.AddSeconds(-viewSeconds).ToOADate(), now.ToOADate());

            if (hasData)
            {
                foreach (var axisIndex in activeAxisIndexes)
                {
                    if (axisMap.TryGetValue(axisIndex, out var axis))
                    {
                        plot.Axes.AutoScaleY(axis);
                    }
                }
            }
        }

        _avaPlot.Refresh();
        UpdateStatusText();
    }

    private Dictionary<int, IYAxis> CreateAxisMap(Plot plot, IReadOnlyCollection<int> activeAxisIndexes)
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
            _yAxis2.IsVisible = activeAxisIndexes.Contains(2);
        }

        if (_yAxis3 is not null)
        {
            axisMap[3] = _yAxis3;
            _yAxis3.IsVisible = activeAxisIndexes.Contains(3);
        }

        if (_yAxis4 is not null)
        {
            axisMap[4] = _yAxis4;
            _yAxis4.IsVisible = activeAxisIndexes.Contains(4);
        }

        return axisMap;
    }

    private List<ChartSeriesConfiguration> GetSeriesConfigurations()
    {
        return _chartItem is null
            ? []
            : CreateChartStateConfiguration(_chartItem).SeriesConfigurations;
    }

    private static List<ChartSeriesConfiguration> ParseSeriesDefinitions(string? raw)
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

            var connectStyle = parts.Length > 2 ? ParseConnectStyle(parts[2]) : ConnectStyle.Straight;
            result.Add(CreateSeriesConfiguration(parts[0], axisIndex, connectStyle));
        }

        return result;
    }

    private static ChartSeriesConfiguration CreateSeriesConfiguration(string targetPath, int axisIndex, ConnectStyle connectStyle = ConnectStyle.Straight)
    {
        var normalizedAxis = Math.Clamp(axisIndex, 1, 4);
        var displayName = targetPath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? targetPath;
        var styleKey = connectStyle switch
        {
            ConnectStyle.StepHorizontal => "Step",
            ConnectStyle.StepVertical => "StepVertical",
            _ => "Line"
        };
        return new ChartSeriesConfiguration(targetPath, normalizedAxis, connectStyle, $"{targetPath}|Y{normalizedAxis}|{styleKey}", $"Y{normalizedAxis} {displayName}");
    }

    private static ConnectStyle ParseConnectStyle(string? style)
    {
        if (string.IsNullOrWhiteSpace(style))
        {
            return ConnectStyle.Straight;
        }

        return style.Trim().ToLowerInvariant() switch
        {
            "step" => ConnectStyle.StepHorizontal,
            "stephorizontal" => ConnectStyle.StepHorizontal,
            "stepvertical" => ConnectStyle.StepVertical,
            "line" => ConnectStyle.Straight,
            "straight" => ConnectStyle.Straight,
            _ => ConnectStyle.Straight
        };
    }

    private void UpdateStatusText()
    {
        return;
    }

    private void OnPlotPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!IsPaused || _plotHost is null)
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
        if (_avaPlot is null || PlotSyncRoot is not { } syncRoot)
        {
            return false;
        }

        Coordinates coordinates;
        lock (syncRoot)
        {
            var plot = _avaPlot.Plot;
            coordinates = plot.GetCoordinates((float)position.X, (float)position.Y, plot.Axes.Bottom, plot.Axes.Left);
        }

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
        if (_chartState is null)
        {
            point = default;
            return false;
        }

        return _chartState.TryGetNearestPoint(key, xPosition, out point);
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

        switch (rawValue)
        {
            case byte byteValue:
                value = byteValue;
                break;
            case sbyte sbyteValue:
                value = sbyteValue;
                break;
            case short shortValue:
                value = shortValue;
                break;
            case ushort ushortValue:
                value = ushortValue;
                break;
            case int intValue:
                value = intValue;
                break;
            case uint uintValue:
                value = uintValue;
                break;
            case long longValue:
                value = longValue;
                break;
            case ulong ulongValue:
                value = ulongValue;
                break;
            case float floatValue:
                value = floatValue;
                break;
            case double doubleValue:
                value = doubleValue;
                break;
            case decimal decimalValue:
                value = (double)decimalValue;
                break;
            case string textValue:
                if (!double.TryParse(textValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value)
                    && !double.TryParse(textValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out value))
                {
                    return false;
                }

                break;
            default:
                if (rawValue is IConvertible convertible)
                {
                    try
                    {
                        value = convertible.ToDouble(CultureInfo.InvariantCulture);
                        break;
                    }
                    catch
                    {
                        return false;
                    }
                }

                return false;
        }

        return !double.IsNaN(value) && !double.IsInfinity(value);
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

    private void OnInteractivePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        HandleInteractivePointerPressed(e);
    }

    private void OnPauseClicked(object? sender, RoutedEventArgs e)
    {
        IsPaused = !IsPaused;
        e.Handled = true;
    }

    private void OnClearClicked(object? sender, RoutedEventArgs e)
    {
        _chartState?.Clear();
        _chartState?.SampleCurrentValues();
        HideCrosshair();
        RenderPlot();
        e.Handled = true;
    }

    private readonly record struct ChartPoint(DateTime Timestamp, double Value);

    private sealed record ChartSeriesConfiguration(string TargetPath, int AxisIndex, ConnectStyle ConnectStyle, string Key, string DisplayName);

    private sealed record ChartSeriesSnapshot(ChartSeriesConfiguration Configuration, ChartPoint[] Points);

    private sealed record ChartStateConfiguration(int HistorySeconds, int RefreshRateMs, List<ChartSeriesConfiguration> SeriesConfigurations);

    private static ChartRuntimeState GetOrCreateChartState(PageItemModel item)
    {
        lock (ChartStatesLock)
        {
            if (!ChartStates.TryGetValue(item.Id, out var state))
            {
                state = new ChartRuntimeState(item);
                ChartStates[item.Id] = state;
            }
            else
            {
                state.Attach(item);
            }

            return state;
        }
    }

    private static ChartStateConfiguration CreateChartStateConfiguration(PageItemModel item)
    {
        var seriesConfigurations = ParseSeriesDefinitions(item.ChartSeriesDefinitions);
        if (seriesConfigurations.Count == 0 && !string.IsNullOrWhiteSpace(item.TargetPath))
        {
            seriesConfigurations = [CreateSeriesConfiguration(item.TargetPath, 1)];
        }

        return new ChartStateConfiguration(
            Math.Max(1, item.HistorySeconds),
            Math.Max(30, item.RefreshRateMs <= 0 ? 30 : item.RefreshRateMs),
            seriesConfigurations);
    }

    private sealed class ChartRuntimeState
    {
        private readonly object _syncRoot = new();
        private System.Threading.Timer? _sampleTimer;
        private Dictionary<string, List<ChartPoint>> _seriesPoints = new(StringComparer.Ordinal);
        private List<ChartSeriesConfiguration> _seriesConfigurations = [];
        private int _historySeconds;
        private int _refreshRateMs;

        public ChartRuntimeState(PageItemModel item)
        {
            UpdateConfiguration(CreateChartStateConfiguration(item));
            SampleCurrentValues();
        }

        public void Attach(PageItemModel item)
        {
            UpdateConfiguration(CreateChartStateConfiguration(item));
        }

        public void UpdateConfiguration(ChartStateConfiguration configuration)
        {
            lock (_syncRoot)
            {
                _historySeconds = configuration.HistorySeconds;
                _refreshRateMs = configuration.RefreshRateMs;
                _seriesConfigurations = configuration.SeriesConfigurations;

                var nextSeriesPoints = new Dictionary<string, List<ChartPoint>>(StringComparer.Ordinal);
                foreach (var configurationEntry in _seriesConfigurations)
                {
                    nextSeriesPoints[configurationEntry.Key] = _seriesPoints.TryGetValue(configurationEntry.Key, out var existing)
                        ? existing
                        : [];
                }

                _seriesPoints = nextSeriesPoints;
                TrimSeriesLocked(DateTime.Now);

                _sampleTimer ??= new System.Threading.Timer(OnSampleTimerTick, null, Timeout.Infinite, Timeout.Infinite);
                _sampleTimer.Change(_refreshRateMs, _refreshRateMs);
            }
        }

        public void SampleCurrentValues()
        {
            List<ChartSeriesConfiguration> configurations;
            lock (_syncRoot)
            {
                configurations = [.. _seriesConfigurations];
            }

            if (configurations.Count == 0)
            {
                return;
            }

            var sampledAt = DateTime.Now;
            var samples = new List<(ChartSeriesConfiguration Configuration, double Value)>();
            foreach (var configuration in configurations)
            {
                if (!HostRegistries.Data.TryGet(configuration.TargetPath, out var item) || item is null)
                {
                    continue;
                }

                if (!TryResolveNumericValue(item, out var value))
                {
                    continue;
                }

                samples.Add((configuration, value));
            }

            if (samples.Count == 0)
            {
                return;
            }

            lock (_syncRoot)
            {
                foreach (var sample in samples)
                {
                    if (!_seriesPoints.TryGetValue(sample.Configuration.Key, out var points))
                    {
                        points = [];
                        _seriesPoints[sample.Configuration.Key] = points;
                    }

                    points.Add(new ChartPoint(sampledAt, sample.Value));
                }

                TrimSeriesLocked(sampledAt);
            }
        }

        public List<ChartSeriesSnapshot> GetSeriesSnapshots()
        {
            lock (_syncRoot)
            {
                TrimSeriesLocked(DateTime.Now);
                return _seriesConfigurations
                    .Select(configuration => new ChartSeriesSnapshot(
                        configuration,
                        _seriesPoints.TryGetValue(configuration.Key, out var points) ? [.. points] : []))
                    .ToList();
            }
        }

        public bool TryGetNearestPoint(string key, double xPosition, out ChartPoint point)
        {
            lock (_syncRoot)
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

        public void Clear()
        {
            lock (_syncRoot)
            {
                foreach (var points in _seriesPoints.Values)
                {
                    points.Clear();
                }
            }
        }

        private void OnSampleTimerTick(object? state)
        {
            SampleCurrentValues();
        }

        private void TrimSeriesLocked(DateTime now)
        {
            var cutoff = now.AddSeconds(-_historySeconds);
            foreach (var series in _seriesPoints.Values)
            {
                series.RemoveAll(point => point.Timestamp < cutoff);
            }
        }
    }
}

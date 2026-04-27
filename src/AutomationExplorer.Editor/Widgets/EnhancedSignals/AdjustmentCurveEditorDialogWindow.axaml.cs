using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Amium.UiEditor.Models;
using Amium.UiEditor.ViewModels;

namespace Amium.UiEditor.Widgets;

public partial class AdjustmentCurveEditorDialogWindow : Window
{
    private AdjustmentCurveEditorState? _result;
    private int _draggingSplineMarkerId = -1;

    public AdjustmentCurveEditorDialogWindow()
    {
        ViewModel = new AdjustmentCurveEditorDialogViewModel(
            null,
            new AdjustmentCurveEditorState(
                0d,
                1d,
                Array.Empty<ExtendedSignalSplinePoint>(),
                ExtendedSignalSplineInterpolationMode.Linear));
        DataContext = ViewModel;
        InitializeComponent();
    }

    public AdjustmentCurveEditorDialogWindow(MainWindowViewModel? mainWindowViewModel, AdjustmentCurveEditorState state)
        : this()
    {
        ViewModel = new AdjustmentCurveEditorDialogViewModel(mainWindowViewModel, state);
        DataContext = ViewModel;
    }

    public AdjustmentCurveEditorDialogViewModel ViewModel { get; private set; }

    public static async Task<AdjustmentCurveEditorState?> ShowAsync(Window owner, MainWindowViewModel? mainWindowViewModel, AdjustmentCurveEditorState state)
    {
        var dialog = new AdjustmentCurveEditorDialogWindow(mainWindowViewModel, state);
        return await dialog.ShowDialog<AdjustmentCurveEditorState?>(owner);
    }

    private void OnPreviewSectionToggleClicked(object? sender, RoutedEventArgs e)
    {
        ViewModel.IsPreviewSectionExpanded = !ViewModel.IsPreviewSectionExpanded;
        e.Handled = true;
    }

    private void OnDataSectionToggleClicked(object? sender, RoutedEventArgs e)
    {
        ViewModel.IsDataSectionExpanded = !ViewModel.IsDataSectionExpanded;
        e.Handled = true;
    }

    private void OnAddSplinePointClicked(object? sender, RoutedEventArgs e)
    {
        ViewModel.AddSplinePoint();
        e.Handled = true;
    }

    private void OnRemoveSplinePointClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: AdjustmentSplinePointRow row })
        {
            ViewModel.RemoveSplinePoint(row);
        }

        e.Handled = true;
    }

    private void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        if (!ViewModel.TryBuildState(out var state, out var errorMessage))
        {
            ViewModel.ErrorMessage = errorMessage;
            e.Handled = true;
            return;
        }

        _result = state;
        Close(_result);
        e.Handled = true;
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        Close((AdjustmentCurveEditorState?)null);
        e.Handled = true;
    }

    private void OnPreviewMarkerPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: CurvePlotMarker marker })
        {
            return;
        }

        if (!ViewModel.CanDragSplineMarker(marker.MarkerId))
        {
            return;
        }

        _draggingSplineMarkerId = marker.MarkerId;
        if (this.FindControl<Canvas>("PreviewCanvas") is { } previewCanvas)
        {
            e.Pointer.Capture(previewCanvas);
        }

        e.Handled = true;
    }

    private void OnPreviewCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_draggingSplineMarkerId < 0)
        {
            return;
        }

        UpdateDraggedMarkerPosition(e);
        e.Handled = true;
    }

    private void OnPreviewCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_draggingSplineMarkerId < 0)
        {
            return;
        }

        e.Pointer.Capture(null);
        _draggingSplineMarkerId = -1;
        e.Handled = true;
    }

    private void OnPreviewCanvasPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _draggingSplineMarkerId = -1;
        e.Handled = true;
    }

    private void UpdateDraggedMarkerPosition(PointerEventArgs e)
    {
        if (_draggingSplineMarkerId < 0)
        {
            return;
        }

        var previewCanvas = this.FindControl<Canvas>("PreviewCanvas");
        if (previewCanvas is null)
        {
            return;
        }

        var position = e.GetPosition(previewCanvas);
        ViewModel.TryMoveSplinePointByCanvasPosition(_draggingSplineMarkerId, position.X, position.Y);
    }
}

public sealed class AdjustmentCurveEditorDialogViewModel : ObservableObject
{
    private const double DefaultPlotWidth = 560d;
    private const double DefaultPlotHeight = 420d;
    private const double MarkerDiameter = 14d;
    private const double AxisLabelGap = 6d;
    private const double AxisLabelWidth = 52d;
    private const double AxisLabelHeight = 18d;
    private readonly double _offset;
    private readonly double _gain;
    private int _nextSplineMarkerId = 1;
    private bool _isPreviewSectionExpanded = true;
    private bool _isDataSectionExpanded = true;
    private string _errorMessage = string.Empty;
    private string _previewEmptyMessage = string.Empty;
    private string _selectedSplineInterpolationMode = ExtendedSignalSplineInterpolationMode.Linear.ToString();
    private double _plotAxisX;
    private double _plotAxisY = DefaultPlotHeight - 1d;
    private double _plotOriginX;
    private double _plotOriginY = DefaultPlotHeight - 1d;
    private bool _showPlotOriginMarker;
    private string _plotSummary = string.Empty;
    private double _plotMinX;
    private double _plotMaxX = 1d;
    private double _plotMinY;
    private double _plotMaxY = 1d;
    private bool _plotRangeValid;
    private bool _suppressPlotUpdates;

    public AdjustmentCurveEditorDialogViewModel(MainWindowViewModel? mainWindowViewModel, AdjustmentCurveEditorState state)
    {
        _offset = state.Offset;
        _gain = state.Gain;
        _selectedSplineInterpolationMode = state.SplineInterpolationMode.ToString();

        DialogBackground = mainWindowViewModel?.DialogBackground ?? "#E3E5EE";
        BorderColor = mainWindowViewModel?.CardBorderBrush ?? "#D5D9E0";
        PrimaryTextBrush = mainWindowViewModel?.PrimaryTextBrush ?? "#111827";
        SecondaryTextBrush = mainWindowViewModel?.SecondaryTextBrush ?? "#5E6777";
        ButtonBackground = mainWindowViewModel?.EditPanelButtonBackground ?? "#F8FAFC";
        ButtonBorderBrush = mainWindowViewModel?.EditPanelButtonBorderBrush ?? "#CBD5E1";
        ButtonForeground = mainWindowViewModel?.PrimaryTextBrush ?? "#111827";
        EditorBackground = mainWindowViewModel?.ParameterEditBackgrundColor ?? "#FFFFFF";
        EditorForeground = mainWindowViewModel?.ParameterEditForeColor ?? "#111827";
        ParameterHoverColor = mainWindowViewModel?.ParameterHoverColor ?? "#BDBDBD";
        SectionContentBackground = mainWindowViewModel?.EditorDialogSectionContentBackground ?? "#EEF3F8";
        SectionHeaderForeground = mainWindowViewModel?.EditorDialogSectionHeaderForeground ?? "#111827";
        TabSelectBackColor = mainWindowViewModel?.TabSelectBackColor ?? "#FFF1C4";
        TabSelectForeColor = mainWindowViewModel?.TabSelectForeColor ?? "#000000";
        TabBackColor = mainWindowViewModel?.TabBackColor ?? "#E7E7E7";
        TabForeColor = mainWindowViewModel?.TabForeColor ?? "#111827";
        PlotLineBrush = mainWindowViewModel?.EditorDialogSectionHeaderForeground ?? "#111827";
        PlotMarkerBrush = mainWindowViewModel?.TabSelectBackColor ?? "#FFF1C4";
        PlotGuideBrush = "#F97316";
        PlotAxisBrush = mainWindowViewModel?.PrimaryTextBrush ?? "#111827";
        PlotGridBrush = mainWindowViewModel?.CardBorderBrush ?? "#D5D9E0";
        ErrorBrush = "#B42318";

        SplineRows = [];
        PlotPoints = [];
        PlotMarkers = [];
        PlotHorizontalMarkerGuides = [];
        PlotVerticalMarkerGuides = [];
        PlotHorizontalGridLines = [];
        PlotVerticalGridLines = [];
        PlotXAxisLabels = [];
        PlotYAxisLabels = [];
        SplineInterpolationModeOptions = Enum.GetNames<ExtendedSignalSplineInterpolationMode>();

        foreach (var point in state.SplinePoints)
        {
            AddSplinePointCore(
                point.Input.ToString(CultureInfo.InvariantCulture),
                point.Output.ToString(CultureInfo.InvariantCulture));
        }

        if (SplineRows.Count == 0 && IsSplineEditor)
        {
            AddSplinePointCore("0", "0");
            AddSplinePointCore("10", "10");
        }

        SplineRows.CollectionChanged += OnSplineRowsChanged;
        AttachRows(SplineRows);
        UpdatePlot();
    }

    public ObservableCollection<AdjustmentSplinePointRow> SplineRows { get; }

    public ObservableCollection<Point> PlotPoints { get; }

    public ObservableCollection<CurvePlotMarker> PlotMarkers { get; }

    public ObservableCollection<CurvePlotGuideLine> PlotHorizontalMarkerGuides { get; }

    public ObservableCollection<CurvePlotGuideLine> PlotVerticalMarkerGuides { get; }

    public ObservableCollection<CurvePlotGridLine> PlotHorizontalGridLines { get; }

    public ObservableCollection<CurvePlotGridLine> PlotVerticalGridLines { get; }

    public ObservableCollection<CurvePlotAxisLabel> PlotXAxisLabels { get; }

    public ObservableCollection<CurvePlotAxisLabel> PlotYAxisLabels { get; }

    public IReadOnlyList<string> SplineInterpolationModeOptions { get; }

    public string DialogBackground { get; }

    public string BorderColor { get; }

    public string PrimaryTextBrush { get; }

    public string SecondaryTextBrush { get; }

    public string ButtonBackground { get; }

    public string ButtonBorderBrush { get; }

    public string ButtonForeground { get; }

    public string EditorBackground { get; }

    public string EditorForeground { get; }

    public string ParameterHoverColor { get; }

    public string SectionContentBackground { get; }

    public string SectionHeaderForeground { get; }

    public string TabSelectBackColor { get; }

    public string TabSelectForeColor { get; }

    public string TabBackColor { get; }

    public string TabForeColor { get; }

    public string PlotLineBrush { get; }

    public string PlotMarkerBrush { get; }

    public string PlotGuideBrush { get; }

    public string PlotAxisBrush { get; }

    public string PlotGridBrush { get; }

    public string ErrorBrush { get; }

    public string DialogTitle => "Spline Curve Editor";

    public string DialogDescription => "Edit the nonlinear mapping stage and review the resulting Raw-to-Adjusted preview. The preview already includes the current gain and offset from the main adjustment dialog.";

    public string EditorHint => "Edit support points in raw input order. Choose between linear interpolation and Catmull-Rom smoothing. The preview and runtime use the same interpolation mode.";

    public bool IsSplineEditor => true;

    public bool IsPreviewSectionExpanded
    {
        get => _isPreviewSectionExpanded;
        set
        {
            if (SetProperty(ref _isPreviewSectionExpanded, value))
            {
                RaisePropertyChanged(nameof(PreviewSectionToggleGlyph));
            }
        }
    }

    public bool IsDataSectionExpanded
    {
        get => _isDataSectionExpanded;
        set
        {
            if (SetProperty(ref _isDataSectionExpanded, value))
            {
                RaisePropertyChanged(nameof(DataSectionToggleGlyph));
            }
        }
    }

    public string PreviewSectionToggleGlyph => IsPreviewSectionExpanded ? "▼" : "▶";

    public string DataSectionToggleGlyph => IsDataSectionExpanded ? "▼" : "▶";

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value ?? string.Empty);
    }

    public string PreviewEmptyMessage
    {
        get => _previewEmptyMessage;
        private set => SetProperty(ref _previewEmptyMessage, value ?? string.Empty);
    }

    public bool IsPreviewEmpty => PreviewEmptyMessage.Length > 0;

    public double PlotWidth => DefaultPlotWidth;

    public double PlotHeight => DefaultPlotHeight;

    public double PlotAxisX
    {
        get => _plotAxisX;
        private set => SetProperty(ref _plotAxisX, value);
    }

    public double PlotAxisY
    {
        get => _plotAxisY;
        private set => SetProperty(ref _plotAxisY, value);
    }

    public double PlotOriginX
    {
        get => _plotOriginX;
        private set => SetProperty(ref _plotOriginX, value);
    }

    public double PlotOriginY
    {
        get => _plotOriginY;
        private set => SetProperty(ref _plotOriginY, value);
    }

    public bool ShowPlotOriginMarker
    {
        get => _showPlotOriginMarker;
        private set => SetProperty(ref _showPlotOriginMarker, value);
    }

    public string PlotSummary
    {
        get => _plotSummary;
        private set => SetProperty(ref _plotSummary, value);
    }

    public string SelectedSplineInterpolationMode
    {
        get => _selectedSplineInterpolationMode;
        set
        {
            if (SetProperty(ref _selectedSplineInterpolationMode, value ?? ExtendedSignalSplineInterpolationMode.Linear.ToString()))
            {
                UpdatePlot();
            }
        }
    }

    public void AddSplinePoint()
    {
        var nextInput = SplineRows.Count == 0
            ? 0d
            : ParseOrDefault(SplineRows[^1].InputText, SplineRows.Count * 10d) + 10d;
        var nextOutput = SplineRows.Count == 0
            ? 0d
            : ParseOrDefault(SplineRows[^1].OutputText, nextInput);
        AddSplinePointCore(nextInput.ToString(CultureInfo.InvariantCulture), nextOutput.ToString(CultureInfo.InvariantCulture));
        UpdatePlot();
    }

    public void RemoveSplinePoint(AdjustmentSplinePointRow row)
    {
        SplineRows.Remove(row);
        UpdatePlot();
    }

    public bool CanDragSplineMarker(int markerId)
    {
        return _plotRangeValid && SplineRows.Any(row => row.MarkerId == markerId);
    }

    public bool TryMoveSplinePointByCanvasPosition(int markerId, double canvasX, double canvasY)
    {
        if (!CanDragSplineMarker(markerId))
        {
            return false;
        }

        if (Math.Abs(_gain) < double.Epsilon)
        {
            ErrorMessage = "Spline point dragging is disabled while gain is 0 because preview Y cannot be mapped back into spline output.";
            return false;
        }

        if (!TryReadSplineRows(out var currentRows, out var error))
        {
            ErrorMessage = error;
            return false;
        }

        var targetIndex = -1;
        for (var index = 0; index < currentRows.Count; index++)
        {
            if (currentRows[index].MarkerId == markerId)
            {
                targetIndex = index;
                break;
            }
        }

        if (targetIndex < 0 || targetIndex >= currentRows.Count)
        {
            return false;
        }

        var clampedCanvasX = Math.Clamp(canvasX, 0d, PlotWidth);
        var clampedCanvasY = Math.Clamp(canvasY, 0d, PlotHeight);
        var input = UnscaleX(clampedCanvasX, _plotMinX, _plotMaxX);
        var adjustedOutput = UnscaleY(clampedCanvasY, _plotMinY, _plotMaxY);
        var splineOutput = (adjustedOutput - _offset) / _gain;

        var minInput = targetIndex == 0 ? double.NegativeInfinity : currentRows[targetIndex - 1].Input + 0.001d;
        var maxInput = targetIndex == currentRows.Count - 1 ? double.PositiveInfinity : currentRows[targetIndex + 1].Input - 0.001d;
        input = Math.Clamp(input, minInput, maxInput);

        _suppressPlotUpdates = true;
        try
        {
            currentRows[targetIndex].Row.InputText = input.ToString("0.###", CultureInfo.InvariantCulture);
            currentRows[targetIndex].Row.OutputText = splineOutput.ToString("0.###", CultureInfo.InvariantCulture);
        }
        finally
        {
            _suppressPlotUpdates = false;
        }

        UpdatePlot();
        ErrorMessage = string.Empty;
        return true;
    }

    public bool TryBuildState(out AdjustmentCurveEditorState? state, out string errorMessage)
    {
        state = null;
        errorMessage = string.Empty;

        if (!TryReadSplinePoints(out var points, out errorMessage))
        {
            return false;
        }

        state = new AdjustmentCurveEditorState(
            _offset,
            _gain,
            points,
            ParseSplineInterpolationMode(SelectedSplineInterpolationMode));
        return true;
    }

    private void AddSplinePointCore(string inputText, string outputText)
    {
        var row = new AdjustmentSplinePointRow { MarkerId = _nextSplineMarkerId++, InputText = inputText, OutputText = outputText };
        SplineRows.Add(row);
    }

    private void OnSplineRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        DetachRows(e.OldItems);
        AttachRows(e.NewItems);
        UpdatePlot();
    }

    private void AttachRows(IList? rows)
    {
        if (rows is null)
        {
            return;
        }

        foreach (var row in rows.OfType<INotifyPropertyChanged>())
        {
            row.PropertyChanged += OnEditableRowPropertyChanged;
        }
    }

    private void DetachRows(IList? rows)
    {
        if (rows is null)
        {
            return;
        }

        foreach (var row in rows.OfType<INotifyPropertyChanged>())
        {
            row.PropertyChanged -= OnEditableRowPropertyChanged;
        }
    }

    private void OnEditableRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressPlotUpdates)
        {
            return;
        }

        UpdatePlot();
    }

    private void UpdatePlot()
    {
        ErrorMessage = string.Empty;
        PreviewEmptyMessage = string.Empty;
        PlotPoints.Clear();
        PlotMarkers.Clear();
        PlotHorizontalMarkerGuides.Clear();
        PlotVerticalMarkerGuides.Clear();
        PlotHorizontalGridLines.Clear();
        PlotVerticalGridLines.Clear();
        PlotXAxisLabels.Clear();
        PlotYAxisLabels.Clear();
        _plotRangeValid = false;

        var splineValid = TryReadSplineRows(out var splineRows, out var splineError);
        var splinePoints = splineRows.Select(static row => new ExtendedSignalSplinePoint { Input = row.Input, Output = row.Output }).ToArray();
        if (!splineValid)
        {
            PlotAxisX = 0d;
            PlotAxisY = PlotHeight - 1d;
            PlotOriginX = PlotAxisX;
            PlotOriginY = PlotAxisY;
            ShowPlotOriginMarker = false;
            PlotSummary = splineError;
            PreviewEmptyMessage = "Preview unavailable until all values are valid.";
            RaisePropertyChanged(nameof(IsPreviewEmpty));
            return;
        }

        var samples = BuildPreviewSamples(splinePoints);
        if (samples.Count < 2)
        {
            ShowPlotOriginMarker = false;
            PlotSummary = "Add more editable values to generate a preview curve.";
            PreviewEmptyMessage = "No preview data yet.";
            RaisePropertyChanged(nameof(IsPreviewEmpty));
            return;
        }

        var minX = samples.Min(sample => sample.Input);
        var maxX = samples.Max(sample => sample.Input);
        var minY = samples.Min(sample => sample.Output);
        var maxY = samples.Max(sample => sample.Output);
        var markers = BuildPreviewMarkers(splineRows);
        foreach (var marker in markers)
        {
            minX = Math.Min(minX, marker.Input);
            maxX = Math.Max(maxX, marker.Input);
            minY = Math.Min(minY, marker.Output);
            maxY = Math.Max(maxY, marker.Output);
        }

        ExpandRange(ref minX, ref maxX);
        ExpandRange(ref minY, ref maxY);

        PlotAxisX = Math.Clamp(ScaleX(0d, minX, maxX), 0d, PlotWidth - 2d);
        PlotAxisY = Math.Clamp(ScaleY(0d, minY, maxY), 0d, PlotHeight - 2d);
        PlotOriginX = PlotAxisX - (MarkerDiameter / 2d) + 1d;
        PlotOriginY = PlotAxisY - (MarkerDiameter / 2d) + 1d;
        ShowPlotOriginMarker = true;
        _plotMinX = minX;
        _plotMaxX = maxX;
        _plotMinY = minY;
        _plotMaxY = maxY;
        _plotRangeValid = true;
        BuildGridLines(minX, maxX, minY, maxY);

        foreach (var sample in samples)
        {
            PlotPoints.Add(new Point(
                ScaleX(sample.Input, minX, maxX),
                ScaleY(sample.Output, minY, maxY)));
        }

        foreach (var marker in markers)
        {
            var markerCenterX = ScaleX(marker.Input, minX, maxX);
            var markerCenterY = ScaleY(marker.Output, minY, maxY);
            PlotMarkers.Add(new CurvePlotMarker(
                marker.MarkerId,
                markerCenterX - (MarkerDiameter / 2d),
                markerCenterY - (MarkerDiameter / 2d),
                marker.Input.ToString("0.###", CultureInfo.InvariantCulture)));

            PlotHorizontalMarkerGuides.Add(CreateHorizontalGuideLine(markerCenterX, markerCenterY));
            PlotVerticalMarkerGuides.Add(CreateVerticalGuideLine(markerCenterX, markerCenterY));
        }

        PlotSummary = $"Preview range X: {minX.ToString("0.###", CultureInfo.InvariantCulture)} .. {maxX.ToString("0.###", CultureInfo.InvariantCulture)}, Y: {minY.ToString("0.###", CultureInfo.InvariantCulture)} .. {maxY.ToString("0.###", CultureInfo.InvariantCulture)}. Mode={SelectedSplineInterpolationMode}, Gain={_gain.ToString("0.###", CultureInfo.InvariantCulture)}, Offset={_offset.ToString("0.###", CultureInfo.InvariantCulture)}.";
        RaisePropertyChanged(nameof(IsPreviewEmpty));
    }

    private List<CurvePreviewSample> BuildPreviewSamples(IReadOnlyList<ExtendedSignalSplinePoint> splinePoints)
    {
        var samples = new List<CurvePreviewSample>();
        var ordered = splinePoints.OrderBy(static point => point.Input).ToArray();
        var minInput = ordered.Length == 0 ? -10d : ordered[0].Input;
        var maxInput = ordered.Length == 0 ? 10d : ordered[^1].Input;
        ExpandRange(ref minInput, ref maxInput);

        for (var index = 0; index < 96; index++)
        {
            var raw = minInput + ((maxInput - minInput) * index / 95d);
            var mapped = ApplySpline(raw, ordered, ParseSplineInterpolationMode(SelectedSplineInterpolationMode));
            samples.Add(new CurvePreviewSample(raw, ApplyAffine(mapped)));
        }

        return samples;
    }

    private IReadOnlyList<IndexedCurvePreviewSample> BuildPreviewMarkers(IReadOnlyList<ParsedSplineRow> splineRows)
    {
        return splineRows
            .OrderBy(static row => row.Input)
            .Select(row => new IndexedCurvePreviewSample(row.MarkerId, row.Input, ApplyAffine(row.Output)))
            .ToArray();
    }

    private void ApplySplineRows(IReadOnlyList<ExtendedSignalSplinePoint> points)
    {
        _suppressPlotUpdates = true;
        try
        {
            SplineRows.Clear();
            foreach (var point in points.OrderBy(static point => point.Input))
            {
                AddSplinePointCore(
                    point.Input.ToString("0.###", CultureInfo.InvariantCulture),
                    point.Output.ToString("0.###", CultureInfo.InvariantCulture));
            }
        }
        finally
        {
            _suppressPlotUpdates = false;
        }

        UpdatePlot();
    }

    private double ApplyAffine(double mappedValue) => (mappedValue * _gain) + _offset;

    private bool TryReadSplineRows(out IReadOnlyList<ParsedSplineRow> rows, out string error)
    {
        error = string.Empty;
        rows = Array.Empty<ParsedSplineRow>();

        var result = new List<ParsedSplineRow>(SplineRows.Count);
        for (var index = 0; index < SplineRows.Count; index++)
        {
            var row = SplineRows[index];
            if (!double.TryParse(row.InputText, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var input))
            {
                error = $"Spline point {index + 1} input must be numeric.";
                return false;
            }

            if (!double.TryParse(row.OutputText, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var output))
            {
                error = $"Spline point {index + 1} output must be numeric.";
                return false;
            }

            result.Add(new ParsedSplineRow(row.MarkerId, row, input, output));
        }

        var duplicateInput = result
            .GroupBy(static point => point.Input)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateInput is not null)
        {
            error = $"Spline input '{duplicateInput.Key.ToString(CultureInfo.InvariantCulture)}' is duplicated. Each input value must be unique.";
            return false;
        }

        rows = result.OrderBy(static point => point.Input).ToArray();
        return true;
    }

    private bool TryReadSplinePoints(out IReadOnlyList<ExtendedSignalSplinePoint> points, out string error)
    {
        points = Array.Empty<ExtendedSignalSplinePoint>();
        if (!TryReadSplineRows(out var rows, out error))
        {
            return false;
        }

        points = rows
            .Select(static row => new ExtendedSignalSplinePoint { Input = row.Input, Output = row.Output })
            .ToArray();
        return true;
    }

    private static double ParseOrDefault(string text, double fallback)
    {
        return double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static void ExpandRange(ref double minValue, ref double maxValue)
    {
        if (Math.Abs(maxValue - minValue) < double.Epsilon)
        {
            minValue -= 1d;
            maxValue += 1d;
            return;
        }

        var padding = (maxValue - minValue) * 0.1d;
        minValue -= padding;
        maxValue += padding;
    }

    private void BuildGridLines(double minX, double maxX, double minY, double maxY)
    {
        var xStep = ComputeGridStep(maxX - minX);
        var yStep = ComputeGridStep(maxY - minY);

        for (var x = Math.Ceiling(minX / xStep) * xStep; x <= maxX; x += xStep)
        {
            var normalizedX = NormalizePlotValue(x);
            var scaledX = Math.Clamp(ScaleX(normalizedX, minX, maxX), 0d, PlotWidth - 2d);
            PlotXAxisLabels.Add(CreateXAxisLabel(scaledX, normalizedX));

            if (Math.Abs(normalizedX) < double.Epsilon)
            {
                continue;
            }

            PlotVerticalGridLines.Add(new CurvePlotGridLine(scaledX));
        }

        for (var y = Math.Ceiling(minY / yStep) * yStep; y <= maxY; y += yStep)
        {
            var normalizedY = NormalizePlotValue(y);
            var scaledY = Math.Clamp(ScaleY(normalizedY, minY, maxY), 0d, PlotHeight - 2d);
            PlotYAxisLabels.Add(CreateYAxisLabel(scaledY, normalizedY));

            if (Math.Abs(normalizedY) < double.Epsilon)
            {
                continue;
            }

            PlotHorizontalGridLines.Add(new CurvePlotGridLine(scaledY));
        }
    }

    private CurvePlotGuideLine CreateHorizontalGuideLine(double markerCenterX, double markerCenterY)
    {
        var left = Math.Min(markerCenterX, PlotAxisX);
        var width = Math.Max(Math.Abs(markerCenterX - PlotAxisX), 1d);
        return new CurvePlotGuideLine(left, markerCenterY, width, 1d);
    }

    private CurvePlotGuideLine CreateVerticalGuideLine(double markerCenterX, double markerCenterY)
    {
        var top = Math.Min(markerCenterY, PlotAxisY);
        var height = Math.Max(Math.Abs(markerCenterY - PlotAxisY), 1d);
        return new CurvePlotGuideLine(markerCenterX, top, 1d, height);
    }

    private CurvePlotAxisLabel CreateXAxisLabel(double scaledX, double value)
    {
        var labelY = PlotAxisY <= PlotHeight - AxisLabelHeight - AxisLabelGap
            ? PlotAxisY + AxisLabelGap
            : PlotAxisY - AxisLabelHeight - AxisLabelGap;
        return new CurvePlotAxisLabel(
            ClampPlotPosition(scaledX - (AxisLabelWidth / 2d), PlotWidth - AxisLabelWidth),
            ClampPlotPosition(labelY, PlotHeight - AxisLabelHeight),
            FormatPlotValue(value),
            AxisLabelWidth,
            AxisLabelHeight,
            TextAlignment.Center);
    }

    private CurvePlotAxisLabel CreateYAxisLabel(double scaledY, double value)
    {
        var preferredX = PlotAxisX >= AxisLabelWidth + AxisLabelGap
            ? PlotAxisX - AxisLabelWidth - AxisLabelGap
            : PlotAxisX + AxisLabelGap;
        return new CurvePlotAxisLabel(
            ClampPlotPosition(preferredX, PlotWidth - AxisLabelWidth),
            ClampPlotPosition(scaledY - (AxisLabelHeight / 2d), PlotHeight - AxisLabelHeight),
            FormatPlotValue(value),
            AxisLabelWidth,
            AxisLabelHeight,
            TextAlignment.Right);
    }

    private static double NormalizePlotValue(double value)
        => Math.Abs(value) < 1e-9d ? 0d : value;

    private static string FormatPlotValue(double value)
        => NormalizePlotValue(value).ToString("0.###", CultureInfo.InvariantCulture);

    private static double ClampPlotPosition(double value, double maxValue)
        => Math.Clamp(value, 0d, Math.Max(maxValue, 0d));

    private static double ComputeGridStep(double range)
    {
        var safeRange = Math.Max(Math.Abs(range), 1d);
        var roughStep = safeRange / 8d;
        var magnitude = Math.Pow(10d, Math.Floor(Math.Log10(roughStep)));
        var normalized = roughStep / magnitude;
        var step = normalized switch
        {
            <= 1d => 1d,
            <= 2d => 2d,
            <= 5d => 5d,
            _ => 10d
        };

        return step * magnitude;
    }

    private double ScaleX(double value, double minX, double maxX)
        => ((value - minX) / (maxX - minX)) * PlotWidth;

    private double ScaleY(double value, double minY, double maxY)
        => PlotHeight - (((value - minY) / (maxY - minY)) * PlotHeight);

    private double UnscaleX(double canvasX, double minX, double maxX)
        => minX + ((canvasX / PlotWidth) * (maxX - minX));

    private double UnscaleY(double canvasY, double minY, double maxY)
        => minY + (((PlotHeight - canvasY) / PlotHeight) * (maxY - minY));

    private static double ApplySpline(double value, IReadOnlyList<ExtendedSignalSplinePoint> points, ExtendedSignalSplineInterpolationMode interpolationMode)
    {
        if (points.Count == 0)
        {
            return value;
        }

        var ordered = points.OrderBy(static point => point.Input).ToArray();
        if (interpolationMode == ExtendedSignalSplineInterpolationMode.CatmullRom && ordered.Length >= 3)
        {
            return ApplyCatmullRomSpline(value, ordered);
        }

        return ApplyLinearSpline(value, ordered);
    }

    private static double ApplyLinearSpline(double value, IReadOnlyList<ExtendedSignalSplinePoint> ordered)
    {
        if (value <= ordered[0].Input)
        {
            return ordered[0].Output;
        }

        for (var index = 1; index < ordered.Count; index++)
        {
            var left = ordered[index - 1];
            var right = ordered[index];
            if (value <= right.Input)
            {
                var range = right.Input - left.Input;
                if (Math.Abs(range) < double.Epsilon)
                {
                    return right.Output;
                }

                var progress = (value - left.Input) / range;
                return left.Output + ((right.Output - left.Output) * progress);
            }
        }

        return ordered[^1].Output;
    }

    private static double ApplyCatmullRomSpline(double value, IReadOnlyList<ExtendedSignalSplinePoint> ordered)
    {
        if (value <= ordered[0].Input)
        {
            return ordered[0].Output;
        }

        for (var index = 1; index < ordered.Count; index++)
        {
            var current = ordered[index];
            if (value > current.Input)
            {
                continue;
            }

            var p0 = ordered[Math.Max(0, index - 2)];
            var p1 = ordered[index - 1];
            var p2 = current;
            var p3 = ordered[Math.Min(ordered.Count - 1, index + 1)];
            var range = p2.Input - p1.Input;
            if (Math.Abs(range) < double.Epsilon)
            {
                return p2.Output;
            }

            var t = Math.Clamp((value - p1.Input) / range, 0d, 1d);
            var t2 = t * t;
            var t3 = t2 * t;
            var m1 = ComputeCatmullRomTangent(p0, p1, p2);
            var m2 = ComputeCatmullRomTangent(p1, p2, p3);
            var h00 = (2d * t3) - (3d * t2) + 1d;
            var h10 = t3 - (2d * t2) + t;
            var h01 = (-2d * t3) + (3d * t2);
            var h11 = t3 - t2;

            return (h00 * p1.Output)
                + (h10 * range * m1)
                + (h01 * p2.Output)
                + (h11 * range * m2);
        }

        return ordered[^1].Output;
    }

    private static double ComputeCatmullRomTangent(ExtendedSignalSplinePoint previous, ExtendedSignalSplinePoint current, ExtendedSignalSplinePoint next)
    {
        var leftRange = current.Input - previous.Input;
        var rightRange = next.Input - current.Input;

        if (Math.Abs(leftRange) < double.Epsilon && Math.Abs(rightRange) < double.Epsilon)
        {
            return 0d;
        }

        if (Math.Abs(leftRange) < double.Epsilon)
        {
            return (next.Output - current.Output) / rightRange;
        }

        if (Math.Abs(rightRange) < double.Epsilon)
        {
            return (current.Output - previous.Output) / leftRange;
        }

        var leftSlope = (current.Output - previous.Output) / leftRange;
        var rightSlope = (next.Output - current.Output) / rightRange;
        return ((rightRange * leftSlope) + (leftRange * rightSlope)) / (leftRange + rightRange);
    }

    private static ExtendedSignalSplineInterpolationMode ParseSplineInterpolationMode(string? text)
    {
        return Enum.TryParse<ExtendedSignalSplineInterpolationMode>(text, true, out var mode)
            ? mode
            : ExtendedSignalSplineInterpolationMode.Linear;
    }

    private readonly record struct CurvePreviewSample(double Input, double Output);

    private readonly record struct ParsedSplineRow(int MarkerId, AdjustmentSplinePointRow Row, double Input, double Output);

    private readonly record struct IndexedCurvePreviewSample(int MarkerId, double Input, double Output);
}

public sealed class AdjustmentSplinePointRow : ObservableObject
{
    private string _inputText = "0";
    private string _outputText = "0";

    public int MarkerId { get; init; }

    public string InputText
    {
        get => _inputText;
        set => SetProperty(ref _inputText, value ?? string.Empty);
    }

    public string OutputText
    {
        get => _outputText;
        set => SetProperty(ref _outputText, value ?? string.Empty);
    }
}

public sealed record CurvePlotMarker(int MarkerId, double X, double Y, string InputLabel);

public sealed record CurvePlotGuideLine(double Left, double Top, double Width, double Height);

public sealed record CurvePlotGridLine(double Offset);

public sealed record CurvePlotAxisLabel(double Left, double Top, string Text, double Width, double Height, TextAlignment Alignment);
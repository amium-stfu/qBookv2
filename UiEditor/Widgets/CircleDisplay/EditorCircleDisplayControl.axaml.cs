using System;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Amium.UiEditor.Controls;
using Amium.Host;
using Amium.Items;
using Amium.UiEditor.Models;
using Amium.UiEditor.ViewModels;
using TableCellSlot = Amium.UiEditor.Models.FolderItemModel.TableCellSlot;

namespace Amium.UiEditor.Widgets;

public partial class EditorCircleDisplayControl : EditorTemplateWidget
{
    private const string DefaultSignalColor = "#FFC107";
    private const string DefaultProgressBarColor = "#FFC107";
    private const string SignalColorItemName = "SignalColor";
    private const string SignalRunItemName = "SignalRun";
    private const string ProgressBarItemName = "ProgressBar";
    private const string ProgressStateItemName = "ProgressState";
    private const string ProgressBarColorItemName = "ProgressBarColor";
    private const double BeaconStep = 1.6d;
    private const double BeaconSweepDegrees = 34d;

    private Grid? _tableRoot;
    private Grid? _circleSurface;
    private Grid? _contentHost;
    private Grid? _itemContentGrid;
    private Grid? _itemOverlayGrid;
    private Border? _selectionOverlay;
    private Ellipse? _outerRing;
    private Ellipse? _innerRing;
    private Avalonia.Controls.Shapes.Path? _progressArc;
    private Avalonia.Controls.Shapes.Path? _beaconArc;
    private INotifyCollectionChanged? _itemsCollection;
    private INotifyPropertyChanged? _itemPropertySource;
    private readonly DispatcherTimer _beaconTimer;
    private bool _isSelecting;
    private int _selectionAnchorRow;
    private int _selectionAnchorColumn;
    private int _selectionCurrentRow;
    private int _selectionCurrentColumn;
    private bool _hasRangeAnchor;
    private int _rangeAnchorRow;
    private int _rangeAnchorColumn;
    private Cursor? _previousCursor;
    private double _beaconOffset;
    private bool _isBeaconRunning;
    private bool _isProgressBarActive;
    private double _currentProgressState;
    private string _registryPath = string.Empty;

    private FolderItemModel? Item => DataContext as FolderItemModel;

    private MainWindowViewModel? ViewModel
        => this.GetVisualRoot() is Window { DataContext: MainWindowViewModel viewModel } ? viewModel : null;

    public EditorCircleDisplayControl()
    {
        InitializeComponent();
        _beaconTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(24)
        };
        _beaconTimer.Tick += OnBeaconTimerTick;
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _tableRoot = this.FindControl<Grid>("TableRoot");
        _circleSurface = this.FindControl<Grid>("CircleSurface");
        _contentHost = this.FindControl<Grid>("ContentHost");
        _itemContentGrid = this.FindControl<Grid>("ItemContentGrid");
        _itemOverlayGrid = this.FindControl<Grid>("ItemOverlayGrid");
        _selectionOverlay = this.FindControl<Border>("SelectionOverlay");
        _outerRing = this.FindControl<Ellipse>("OuterRing");
        _innerRing = this.FindControl<Ellipse>("InnerRing");
        _progressArc = this.FindControl<Avalonia.Controls.Shapes.Path>("ProgressArc");
        _beaconArc = this.FindControl<Avalonia.Controls.Shapes.Path>("BeaconArc");

        if (_tableRoot is not null)
        {
            _tableRoot.SizeChanged += OnRootSizeChanged;
        }

        HookItemsCollection();
        EnsureRuntimeSignals();
        RefreshSignalState();
        RefreshCircleGeometry();
        RefreshItemOverlay();
        HostRegistries.Data.ItemChanged += OnRegistryItemChanged;
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        HostRegistries.Data.ItemChanged -= OnRegistryItemChanged;
        if (_tableRoot is not null)
        {
            _tableRoot.SizeChanged -= OnRootSizeChanged;
        }

        UnhookItemProperties();
        UnhookItemsCollection();
        StopBeacon();
        _tableRoot = null;
        _circleSurface = null;
        _contentHost = null;
        _itemContentGrid = null;
        _itemOverlayGrid = null;
        _selectionOverlay = null;
        _outerRing = null;
        _innerRing = null;
        _progressArc = null;
        _beaconArc = null;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        HookItemProperties();
        HookItemsCollection();
        EnsureRuntimeSignals();
        RefreshSignalState();
        RefreshCircleGeometry();
        RefreshItemOverlay();
    }

    private void OnRootSizeChanged(object? sender, SizeChangedEventArgs e) => RefreshCircleGeometry();

    private void HookItemsCollection()
    {
        UnhookItemsCollection();
        _itemsCollection = Item?.Items;
        if (_itemsCollection is not null)
        {
            _itemsCollection.CollectionChanged += OnItemsCollectionChanged;
        }
    }

    private void UnhookItemsCollection()
    {
        if (_itemsCollection is null)
        {
            return;
        }

        _itemsCollection.CollectionChanged -= OnItemsCollectionChanged;
        _itemsCollection = null;
    }

    private void HookItemProperties()
    {
        UnhookItemProperties();
        _itemPropertySource = Item;
        if (_itemPropertySource is not null)
        {
            _itemPropertySource.PropertyChanged += OnItemPropertyChanged;
        }
    }

    private void UnhookItemProperties()
    {
        if (_itemPropertySource is null)
        {
            return;
        }

        _itemPropertySource.PropertyChanged -= OnItemPropertyChanged;
        _itemPropertySource = null;
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshItemOverlay();
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.PropertyName)
            || string.Equals(e.PropertyName, nameof(FolderItemModel.TableRows), StringComparison.Ordinal)
            || string.Equals(e.PropertyName, nameof(FolderItemModel.TableColumns), StringComparison.Ordinal))
        {
            RefreshItemOverlay();
        }

        if (string.IsNullOrWhiteSpace(e.PropertyName)
            || string.Equals(e.PropertyName, nameof(FolderItemModel.Path), StringComparison.Ordinal)
            || string.Equals(e.PropertyName, nameof(FolderItemModel.Name), StringComparison.Ordinal)
            || string.Equals(e.PropertyName, nameof(FolderItemModel.Title), StringComparison.Ordinal)
            || string.Equals(e.PropertyName, nameof(FolderItemModel.SignalColor), StringComparison.Ordinal)
            || string.Equals(e.PropertyName, nameof(FolderItemModel.SignalRun), StringComparison.Ordinal)
            || string.Equals(e.PropertyName, nameof(FolderItemModel.ProgressBar), StringComparison.Ordinal)
            || string.Equals(e.PropertyName, nameof(FolderItemModel.ProgressState), StringComparison.Ordinal)
            || string.Equals(e.PropertyName, nameof(FolderItemModel.ProgressBarColor), StringComparison.Ordinal))
        {
            EnsureRuntimeSignals();
            RefreshSignalState();
        }
    }

    private void RefreshCircleGeometry()
    {
        if (_tableRoot is null || _circleSurface is null || _contentHost is null || _outerRing is null || _innerRing is null || _progressArc is null || _beaconArc is null)
        {
            return;
        }

        var size = Math.Max(0d, Math.Min(_tableRoot.Bounds.Width, _tableRoot.Bounds.Height));
        _circleSurface.Width = size;
        _circleSurface.Height = size;

        var ringThickness = Math.Max(6d, size * 0.035d);
        var outerMargin = ringThickness;
        var innerMargin = ringThickness * 2.1d;
        var contentMargin = ringThickness * 3.35d;
        var outerStrokeThickness = Math.Max(2d, ringThickness * 0.28d);
        var innerStrokeThickness = Math.Max(2d, ringThickness * 0.22d);
        var outerRadius = (size / 2d) - outerMargin - (outerStrokeThickness / 2d);
        var innerRadius = (size / 2d) - innerMargin - (innerStrokeThickness / 2d);
        var beaconBand = Math.Max(2d, outerRadius - innerRadius);

        _outerRing.Margin = new Thickness(outerMargin);
        _outerRing.StrokeThickness = outerStrokeThickness;

        _innerRing.Margin = new Thickness(innerMargin);
        _innerRing.StrokeThickness = innerStrokeThickness;

        _progressArc.Margin = default;
        _beaconArc.Margin = default;
        _progressArc.StrokeThickness = beaconBand * 0.58d;
        _beaconArc.StrokeThickness = beaconBand * 0.58d;

        _contentHost.Margin = new Thickness(contentMargin);
        _contentHost.Clip = new EllipseGeometry(new Rect(_contentHost.Bounds.Size));
        UpdateProgressVisual();
        UpdateActivityVisual();
        RefreshItemOverlay();
    }

    private void RefreshItemOverlay()
    {
        if (_itemContentGrid is null || _itemOverlayGrid is null || Item is null)
        {
            return;
        }

        _itemContentGrid.RowDefinitions.Clear();
        _itemContentGrid.ColumnDefinitions.Clear();
        _itemOverlayGrid.RowDefinitions.Clear();
        _itemOverlayGrid.ColumnDefinitions.Clear();

        for (var row = 0; row < Item.TableRows; row++)
        {
            _itemContentGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            _itemOverlayGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        }

        for (var column = 0; column < Item.TableColumns; column++)
        {
            _itemContentGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            _itemOverlayGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        _itemContentGrid.Children.Clear();
        _itemOverlayGrid.Children.Clear();

        foreach (var child in Item.Items)
        {
            if (!child.IsTableChildControl)
            {
                continue;
            }

            var row = Math.Max(0, child.TableCellRow - 1);
            var column = Math.Max(0, child.TableCellColumn - 1);
            var rowSpan = Math.Max(1, child.TableCellRowSpan);
            var columnSpan = Math.Max(1, child.TableCellColumnSpan);

            Control content;
            if (child.IsButton)
            {
                content = new EditorButtonControl();
            }
            else if (child.IsItem)
            {
                content = new EditorSignalControl();
            }
            else if (child.IsLogControl)
            {
                content = new EditorLogControl
                {
                    PageIsActive = true
                };
            }
            else if (child.IsChartControl)
            {
                content = new RealtimeChartControl
                {
                    PageIsActive = true
                };
            }
            else if (child.IsUdlClientControl)
            {
                content = new UdlClientControl();
            }
            else
            {
                continue;
            }

            content.DataContext = child;
            content.HorizontalAlignment = HorizontalAlignment.Stretch;
            content.VerticalAlignment = VerticalAlignment.Stretch;
            content.Bind(Control.IsVisibleProperty, new Binding(nameof(FolderItemModel.IsVisibleInActiveView)));

            Grid.SetRow(content, row);
            Grid.SetColumn(content, column);
            Grid.SetRowSpan(content, rowSpan);
            Grid.SetColumnSpan(content, columnSpan);
            _itemContentGrid.Children.Add(content);

            var overlay = new Grid
            {
                DataContext = child,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            overlay.Bind(IsVisibleProperty, new Binding(nameof(FolderItemModel.IsSelected)));

            var toolbar = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(4, 4, 4, 0),
                Background = Brushes.Orange,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(2, 1, 2, 1),
                Opacity = 0.95
            };

            var stack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 2
            };

            var settingsButton = new Button
            {
                Width = 16,
                Height = 16,
                MinWidth = 16,
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                Tag = child
            };
            settingsButton.Classes.Add("toolicon");
            ToolTip.SetTip(settingsButton, "Settings");
            settingsButton.Click += OnChildSettingsClicked;
            settingsButton.Content = new ThemeSvgIcon
            {
                Width = 10,
                Height = 10,
                IconPath = "avares://AutomationExplorer.Editor/EditorIcons/cog.svg"
            };

            var deleteButton = new Button
            {
                Width = 16,
                Height = 16,
                MinWidth = 16,
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                Tag = child
            };
            deleteButton.Classes.Add("toolicon");
            ToolTip.SetTip(deleteButton, "Delete");
            deleteButton.Click += OnChildDeleteClicked;
            deleteButton.Content = new ThemeSvgIcon
            {
                Width = 10,
                Height = 10,
                IconPath = "avares://AutomationExplorer.Editor/EditorIcons/remove.svg"
            };

            stack.Children.Add(settingsButton);
            stack.Children.Add(deleteButton);
            toolbar.Child = stack;
            overlay.Children.Add(toolbar);

            Grid.SetRow(overlay, row);
            Grid.SetColumn(overlay, column);
            Grid.SetRowSpan(overlay, rowSpan);
            Grid.SetColumnSpan(overlay, columnSpan);
            _itemOverlayGrid.Children.Add(overlay);
        }
    }

    private void EnsureRuntimeSignals()
    {
        if (Item is null || string.IsNullOrWhiteSpace(Item.Path))
        {
            _registryPath = string.Empty;
            return;
        }

        _registryPath = Item.Path;
        var segments = _registryPath.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return;
        }

        var nameSegment = segments[^1];
        var parentPath = segments.Length > 1 ? string.Join('.', segments, 0, segments.Length - 1) : null;
        var signalColor = string.IsNullOrWhiteSpace(Item.SignalColor) ? DefaultSignalColor : Item.SignalColor;
        var signalRun = Item.SignalRun;
        var progressBar = Item.ProgressBar;
        var progressState = Math.Clamp(Item.ProgressState, 0d, 100d);
        var progressBarColor = string.IsNullOrWhiteSpace(Item.ProgressBarColor) ? DefaultProgressBarColor : Item.ProgressBarColor;

        Item snapshot;
        if (HostRegistries.Data.TryGet(_registryPath, out var existing) && existing is not null)
        {
            snapshot = existing.Clone();
        }
        else
        {
            snapshot = string.IsNullOrWhiteSpace(parentPath)
                ? new Item(nameSegment)
                : new Item(nameSegment, null, parentPath);
        }

        snapshot.Params["Path"].Value = _registryPath;
        snapshot.Params["Kind"].Value = "CircleDisplay";
        snapshot.Params["Text"].Value = string.IsNullOrWhiteSpace(Item.Name) ? Item.Title : Item.Name;
        snapshot[SignalColorItemName].Value = signalColor;
        snapshot[SignalColorItemName].Params["Text"].Value = SignalColorItemName;
        snapshot[SignalRunItemName].Value = signalRun;
        snapshot[SignalRunItemName].Params["Text"].Value = SignalRunItemName;
        snapshot[ProgressBarItemName].Value = progressBar;
        snapshot[ProgressBarItemName].Params["Text"].Value = ProgressBarItemName;
        snapshot[ProgressStateItemName].Value = progressState;
        snapshot[ProgressStateItemName].Params["Text"].Value = ProgressStateItemName;
        snapshot[ProgressBarColorItemName].Value = progressBarColor;
        snapshot[ProgressBarColorItemName].Params["Text"].Value = ProgressBarColorItemName;
        HostRegistries.Data.UpsertSnapshot(_registryPath, snapshot, pruneMissingMembers: false);
    }

    private void RefreshSignalState()
    {
        ApplyBeaconColor(DefaultSignalColor);
        ApplyProgressColor(DefaultProgressBarColor);
        ApplyState(false, false, 0d);

        if (string.IsNullOrWhiteSpace(_registryPath))
        {
            return;
        }

        if (!HostRegistries.Data.TryGet(_registryPath, out var item) || item is null)
        {
            return;
        }

        if (item.Has(SignalColorItemName))
        {
            ApplyBeaconColor(item[SignalColorItemName].Value?.ToString());
        }

        if (item.Has(ProgressBarColorItemName))
        {
            ApplyProgressColor(item[ProgressBarColorItemName].Value?.ToString());
        }

        var progressBar = item.Has(ProgressBarItemName) && ToBoolean(item[ProgressBarItemName].Value);
        var progressState = item.Has(ProgressStateItemName) ? ToDouble(item[ProgressStateItemName].Value) : 0d;
        var signalRun = item.Has(SignalRunItemName) && ToBoolean(item[SignalRunItemName].Value);

        ApplyState(progressBar, signalRun, progressState);
    }

    private void OnRegistryItemChanged(object? sender, DataChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_registryPath))
        {
            return;
        }

        if (!string.Equals(e.Key, _registryPath, StringComparison.Ordinal)
            && !e.Key.StartsWith(_registryPath + ".", StringComparison.Ordinal))
        {
            return;
        }

        Dispatcher.UIThread.Post(RefreshSignalState);
    }

    private void ApplyBeaconColor(string? value)
    {
        if (_beaconArc is null)
        {
            return;
        }

        if (!Color.TryParse(string.IsNullOrWhiteSpace(value) ? DefaultSignalColor : value, out var color))
        {
            Color.TryParse(DefaultSignalColor, out color);
        }

        _beaconArc.Stroke = new SolidColorBrush(color);
    }

    private void ApplyProgressColor(string? value)
    {
        if (_progressArc is null)
        {
            return;
        }

        if (!Color.TryParse(string.IsNullOrWhiteSpace(value) ? DefaultProgressBarColor : value, out var color))
        {
            Color.TryParse(DefaultProgressBarColor, out color);
        }

        _progressArc.Stroke = new SolidColorBrush(color);
    }

    private void ApplyState(bool progressBar, bool signalRun, double progressState)
    {
        _isProgressBarActive = progressBar;
        _isBeaconRunning = signalRun;
        _currentProgressState = Math.Clamp(progressState, 0d, 100d);

        if (signalRun)
        {
            StartBeacon();
        }
        else
        {
            StopBeacon();
        }

        UpdateProgressVisual();
        UpdateActivityVisual();
    }

    private void StartBeacon()
    {
        if (!_beaconTimer.IsEnabled)
        {
            _beaconTimer.Start();
        }
    }

    private void StopBeacon()
    {
        _beaconTimer.Stop();
    }

    private void OnBeaconTimerTick(object? sender, EventArgs e)
    {
        _beaconOffset = (_beaconOffset + BeaconStep) % 360d;
        UpdateActivityVisual();
    }

    private void UpdateProgressVisual()
    {
        if (_progressArc is null || _circleSurface is null)
        {
            return;
        }

        if (!_isProgressBarActive || _currentProgressState <= 0d)
        {
            _progressArc.IsVisible = false;
            _progressArc.Data = null;
            return;
        }

        var size = Math.Min(_circleSurface.Bounds.Width, _circleSurface.Bounds.Height);
        var ringThickness = Math.Max(6d, size * 0.035d);
        var outerMargin = ringThickness;
        var innerMargin = ringThickness * 2.1d;
        var outerStrokeThickness = Math.Max(2d, ringThickness * 0.28d);
        var innerStrokeThickness = Math.Max(2d, ringThickness * 0.22d);
        var outerRadius = (size / 2d) - outerMargin - (outerStrokeThickness / 2d);
        var innerRadius = (size / 2d) - innerMargin - (innerStrokeThickness / 2d);
        var radius = Math.Max(1d, (outerRadius + innerRadius) / 2d);
        var center = new Point(_circleSurface.Bounds.Width / 2d, _circleSurface.Bounds.Height / 2d);
        _progressArc.Data = BuildArcGeometry(center, radius, -90d, 360d * (_currentProgressState / 100d));
        _progressArc.IsVisible = true;
        _progressArc.Opacity = 1d;
    }

    private void UpdateActivityVisual()
    {
        if (_beaconArc is null || _circleSurface is null)
        {
            return;
        }

        if (!_isBeaconRunning)
        {
            _beaconArc.IsVisible = false;
            _beaconArc.Data = null;
            return;
        }

        var size = Math.Min(_circleSurface.Bounds.Width, _circleSurface.Bounds.Height);
        var ringThickness = Math.Max(6d, size * 0.035d);
        var outerMargin = ringThickness;
        var innerMargin = ringThickness * 2.1d;
        var outerStrokeThickness = Math.Max(2d, ringThickness * 0.28d);
        var innerStrokeThickness = Math.Max(2d, ringThickness * 0.22d);
        var outerRadius = (size / 2d) - outerMargin - (outerStrokeThickness / 2d);
        var innerRadius = (size / 2d) - innerMargin - (innerStrokeThickness / 2d);
        var innerBand = Math.Max(2d, outerRadius - innerRadius);
        var radius = _isProgressBarActive
            ? Math.Max(1d, (size / 2d) - (ringThickness * 0.28d))
            : Math.Max(1d, (outerRadius + innerRadius) / 2d);
        var strokeThickness = _isProgressBarActive
            ? Math.Max(2d, ringThickness * 0.32d)
            : innerBand * 0.58d;
        var center = new Point(_circleSurface.Bounds.Width / 2d, _circleSurface.Bounds.Height / 2d);

        _beaconArc.StrokeThickness = strokeThickness;
        _beaconArc.Data = BuildArcGeometry(center, radius, _beaconOffset - 90d, BeaconSweepDegrees);
        _beaconArc.IsVisible = true;
        _beaconArc.Opacity = 1d;
    }

    private static PathGeometry? BuildArcGeometry(Point center, double radius, double startAngle, double sweepAngle)
    {
        var clampedSweep = Math.Clamp(sweepAngle, 0d, 360d);
        if (radius <= 0d || clampedSweep <= 0d)
        {
            return null;
        }

        var geometry = new PathGeometry
        {
            Figures = new PathFigures()
        };

        if (clampedSweep >= 359.999d)
        {
            var startPoint = PointOnCircle(center, radius, startAngle);
            var midpoint = PointOnCircle(center, radius, startAngle + 180d);
            var figure = new PathFigure
            {
                StartPoint = startPoint,
                IsClosed = false,
                IsFilled = false,
                Segments = new PathSegments()
            };
            figure.Segments.Add(new ArcSegment
            {
                Point = midpoint,
                Size = new Size(radius, radius),
                IsLargeArc = false,
                SweepDirection = SweepDirection.Clockwise
            });
            figure.Segments.Add(new ArcSegment
            {
                Point = startPoint,
                Size = new Size(radius, radius),
                IsLargeArc = false,
                SweepDirection = SweepDirection.Clockwise
            });
            geometry.Figures.Add(figure);
            return geometry;
        }

        var endAngle = startAngle + clampedSweep;
        var figureSingle = new PathFigure
        {
            StartPoint = PointOnCircle(center, radius, startAngle),
            IsClosed = false,
            IsFilled = false,
            Segments = new PathSegments()
        };
        figureSingle.Segments.Add(new ArcSegment
        {
            Point = PointOnCircle(center, radius, endAngle),
            Size = new Size(radius, radius),
            IsLargeArc = clampedSweep > 180d,
            SweepDirection = SweepDirection.Clockwise
        });
        geometry.Figures.Add(figureSingle);
        return geometry;
    }

    private static Point PointOnCircle(Point center, double radius, double angleDegrees)
    {
        var angleRadians = angleDegrees * Math.PI / 180d;
        return new Point(
            center.X + (Math.Cos(angleRadians) * radius),
            center.Y + (Math.Sin(angleRadians) * radius));
    }

    private static bool ToBoolean(object? value)
    {
        return value switch
        {
            bool boolValue => boolValue,
            string text when bool.TryParse(text, out var parsedBool) => parsedBool,
            string text when long.TryParse(text, out var parsedLong) => parsedLong != 0,
            byte numeric => numeric != 0,
            sbyte numeric => numeric != 0,
            short numeric => numeric != 0,
            ushort numeric => numeric != 0,
            int numeric => numeric != 0,
            uint numeric => numeric != 0,
            long numeric => numeric != 0,
            ulong numeric => numeric != 0,
            float numeric => Math.Abs(numeric) > float.Epsilon,
            double numeric => Math.Abs(numeric) > double.Epsilon,
            decimal numeric => numeric != 0,
            _ => false
        };
    }

    private static double ToDouble(object? value)
    {
        return value switch
        {
            null => 0d,
            double doubleValue => doubleValue,
            float floatValue => floatValue,
            decimal decimalValue => (double)decimalValue,
            int intValue => intValue,
            long longValue => longValue,
            short shortValue => shortValue,
            uint uintValue => uintValue,
            ulong ulongValue => ulongValue,
            byte byteValue => byteValue,
            sbyte sbyteValue => sbyteValue,
            string text when double.TryParse(text, out var parsed) => parsed,
            _ => 0d
        };
    }

    private void OnAddItemClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Control { ContextMenu: { } menu })
        {
            menu.Open();
            e.Handled = true;
        }
    }

    private void OnCellPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ViewModel is null || Item is null || !ViewModel.IsEditMode)
        {
            return;
        }

        if (sender is not Control control || control.DataContext is not TableCellSlot slot || !slot.IsVisibleInLayout)
        {
            return;
        }

        var point = e.GetCurrentPoint(control);
        if (slot.ChildItem is FolderItemModel childItem
            && (point.Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed
                || point.Properties.PointerUpdateKind == PointerUpdateKind.RightButtonPressed))
        {
            ViewModel.SelectItem(childItem);
            e.Handled = true;
            return;
        }

        if (point.Properties.PointerUpdateKind == PointerUpdateKind.RightButtonPressed)
        {
            ViewModel.ToggleTableCellSelection(Item, slot.Row, slot.Column, toggle: false);
            ViewModel.AddItemToSelectedTableCells(Item);
            RefreshItemOverlay();
            e.Handled = true;
            return;
        }

        var modifiers = e.KeyModifiers;
        if (point.Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed)
        {
            _isSelecting = true;
            _selectionAnchorRow = slot.Row;
            _selectionAnchorColumn = slot.Column;
            _selectionCurrentRow = slot.Row;
            _selectionCurrentColumn = slot.Column;
            _previousCursor = control.Cursor;
            control.Cursor = new Cursor(StandardCursorType.Cross);
            e.Pointer.Capture(control);
            UpdateSelectionOverlay(_selectionAnchorRow, _selectionAnchorColumn, _selectionCurrentRow, _selectionCurrentColumn);
        }

        if (point.Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed
            && modifiers.HasFlag(KeyModifiers.Control))
        {
            HandleCtrlRangeClick(slot);
            e.Handled = true;
            return;
        }

        _hasRangeAnchor = false;
        var isToggle = modifiers.HasFlag(KeyModifiers.Shift);
        ViewModel.ToggleTableCellSelection(Item, slot.Row, slot.Column, isToggle);
        e.Handled = true;
    }

    private void OnCellPointerEnter(object? sender, PointerEventArgs e)
    {
        if (!_isSelecting || ViewModel is null || Item is null || !ViewModel.IsEditMode)
        {
            return;
        }

        if (sender is not Control { DataContext: TableCellSlot slot } control || !slot.IsVisibleInLayout)
        {
            return;
        }

        var point = e.GetCurrentPoint(control);
        if (!point.Properties.IsLeftButtonPressed)
        {
            _isSelecting = false;
            if (_selectionOverlay is not null)
            {
                _selectionOverlay.IsVisible = false;
            }
            control.Cursor = _previousCursor;
            return;
        }

        _selectionCurrentRow = slot.Row;
        _selectionCurrentColumn = slot.Column;
        UpdateSelectionOverlay(_selectionAnchorRow, _selectionAnchorColumn, _selectionCurrentRow, _selectionCurrentColumn);
        e.Handled = true;
    }

    private void OnCellPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == MouseButton.Left)
        {
            if (_isSelecting && ViewModel is not null && Item is not null && ViewModel.IsEditMode)
            {
                ViewModel.SelectTableRectangle(Item, _selectionAnchorRow, _selectionAnchorColumn, _selectionCurrentRow, _selectionCurrentColumn);
            }

            _isSelecting = false;
            e.Pointer.Capture(null);

            if (sender is Control control)
            {
                control.Cursor = _previousCursor;
            }

            if (_selectionOverlay is not null)
            {
                _selectionOverlay.IsVisible = false;
            }
        }
    }

    private void HandleCtrlRangeClick(TableCellSlot slot)
    {
        if (ViewModel is null || Item is null || !ViewModel.IsEditMode || !slot.IsVisibleInLayout)
        {
            return;
        }

        if (slot.ChildItem is not null)
        {
            return;
        }

        if (!_hasRangeAnchor)
        {
            _rangeAnchorRow = slot.Row;
            _rangeAnchorColumn = slot.Column;
            _hasRangeAnchor = true;
            ViewModel.SelectTableRectangle(Item, slot.Row, slot.Column, slot.Row, slot.Column);
            return;
        }

        ViewModel.SelectTableRectangle(Item, _rangeAnchorRow, _rangeAnchorColumn, slot.Row, slot.Column);
        _hasRangeAnchor = false;
    }

    private void UpdateSelectionOverlay(int startRow, int startColumn, int endRow, int endColumn)
    {
        if (_selectionOverlay is null || _contentHost is null || Item is null)
        {
            return;
        }

        var minRow = Math.Min(startRow, endRow);
        var maxRow = Math.Max(startRow, endRow);
        var minColumn = Math.Min(startColumn, endColumn);
        var maxColumn = Math.Max(startColumn, endColumn);
        var rootBounds = _contentHost.Bounds;
        if (rootBounds.Width <= 0 || rootBounds.Height <= 0)
        {
            return;
        }

        var cellWidth = rootBounds.Width / Math.Max(1, Item.TableColumns);
        var cellHeight = rootBounds.Height / Math.Max(1, Item.TableRows);
        var x = (minColumn - 1) * cellWidth;
        var y = (minRow - 1) * cellHeight;
        var width = Math.Max(cellWidth, (maxColumn - minColumn + 1) * cellWidth);
        var height = Math.Max(cellHeight, (maxRow - minRow + 1) * cellHeight);

        _selectionOverlay.Margin = new Thickness(x, y, 0, 0);
        _selectionOverlay.Width = width;
        _selectionOverlay.Height = height;
        _selectionOverlay.IsVisible = true;
    }

    private void AddControlToTable(ControlKind kind)
    {
        if (ViewModel is null || Item is null || !ViewModel.IsEditMode)
        {
            return;
        }

        ViewModel.AddControlToSelectedTableCells(Item, kind);
        RefreshItemOverlay();
    }

    private void OnAddItemMenuClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        AddControlToTable(ControlKind.Item);
        e.Handled = true;
    }

    private void OnAddButtonMenuClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        AddControlToTable(ControlKind.Button);
        e.Handled = true;
    }

    private void OnAddChartMenuClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        AddControlToTable(ControlKind.ChartControl);
        e.Handled = true;
    }

    private void OnAddLogMenuClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        AddControlToTable(ControlKind.LogControl);
        e.Handled = true;
    }

    private void OnAddUdlClientMenuClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ViewModel is null || !ViewModel.SupportsUdlClientControl)
        {
            return;
        }

        AddControlToTable(ControlKind.UdlClientControl);
        e.Handled = true;
    }

    private void OnChildSettingsClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ViewModel is null || sender is not Control { Tag: FolderItemModel child })
        {
            return;
        }

        ViewModel.OpenItemEditor(child, 0, 0);
        e.Handled = true;
    }

    private void OnChildDeleteClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Item is null || sender is not Control { Tag: FolderItemModel child })
        {
            return;
        }

        if (Item.Items.Contains(child))
        {
            Item.Items.Remove(child);
            Item.UpdateTableCellContentFromChildren();
            RefreshItemOverlay();
        }

        e.Handled = true;
    }
}

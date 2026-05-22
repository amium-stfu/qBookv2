using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using HornetStudio.Editor.Controls;
using HornetStudio.Editor.Models;
using HornetStudio.Editor.ViewModels;
using HornetStudio.Host;

namespace HornetStudio.Editor.Widgets;

/// <summary>
/// Displays and edits PID controller definitions for a controller widget.
/// </summary>
public partial class ControllerControl : EditorTemplateControl
{
    /// <summary>
    /// Indicates whether the widget currently has no configured controllers.
    /// </summary>
    public static readonly DirectProperty<ControllerControl, bool> HasNoControllersProperty =
        AvaloniaProperty.RegisterDirect<ControllerControl, bool>(nameof(HasNoControllers), control => control.HasNoControllers);

    private FolderItemModel? _observedItem;
    private bool _hasNoControllers = true;
    private int _runtimeRefreshQueued;
    private int _suppressObservedItemRebuild;

    /// <summary>
    /// Initializes a new instance of the <see cref="ControllerControl"/> class.
    /// </summary>
    public ControllerControl()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
        DataContextChanged += OnDataContextChanged;
        Controllers.CollectionChanged += OnControllersCollectionChanged;
    }

    /// <summary>
    /// Gets the currently displayed controller rows.
    /// </summary>
    public ObservableCollection<ControllerRow> Controllers { get; } = [];

    /// <summary>
    /// Gets a value indicating whether no controllers are configured.
    /// </summary>
    public bool HasNoControllers
    {
        get => _hasNoControllers;
        private set => SetAndRaise(HasNoControllersProperty, ref _hasNoControllers, value);
    }

    private FolderItemModel? Item => DataContext as FolderItemModel;

    private MainWindowViewModel? ViewModel => TopLevel.GetTopLevel(this)?.DataContext as MainWindowViewModel;

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        HookObservedItem();
        HostRegistries.Data.ItemChanged += OnRegistryItemChanged;
        RebuildControllers();
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        HostRegistries.Data.ItemChanged -= OnRegistryItemChanged;
        UnhookObservedItem();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        HookObservedItem();
        RebuildControllers();
    }

    private void HookObservedItem()
    {
        if (ReferenceEquals(_observedItem, Item))
        {
            return;
        }

        UnhookObservedItem();
        _observedItem = Item;
        if (_observedItem is not null)
        {
            _observedItem.PropertyChanged += OnObservedItemPropertyChanged;
        }
    }

    private void UnhookObservedItem()
    {
        if (_observedItem is null)
        {
            return;
        }

        _observedItem.PropertyChanged -= OnObservedItemPropertyChanged;
        _observedItem = null;
    }

    private void OnObservedItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            var propertyName = e.PropertyName;
            Dispatcher.UIThread.Post(() => OnObservedItemPropertyChanged(sender, new PropertyChangedEventArgs(propertyName)));
            return;
        }

        if (string.IsNullOrWhiteSpace(e.PropertyName)
            || e.PropertyName == nameof(FolderItemModel.ControllerDefinitions)
            || e.PropertyName == nameof(FolderItemModel.Name)
            || e.PropertyName == nameof(FolderItemModel.Path)
            || e.PropertyName == nameof(FolderItemModel.FolderName))
        {
            if (Volatile.Read(ref _suppressObservedItemRebuild) == 0)
            {
                RebuildControllers();
            }

            return;
        }

        if (e.PropertyName is nameof(FolderItemModel.EffectiveBodyBackground)
            or nameof(FolderItemModel.EffectiveBodyBorder)
            or nameof(FolderItemModel.EffectiveBodyForeground)
            or nameof(FolderItemModel.EffectiveMutedForeground))
        {
            foreach (var row in Controllers)
            {
                row.RefreshTheme();
            }
        }
    }

    private void OnControllersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        HasNoControllers = Controllers.Count == 0;
    }

    private void OnRegistryItemChanged(object? sender, DataChangedEventArgs e)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            QueueRuntimeRefresh();
            return;
        }

        foreach (var row in Controllers)
        {
            if (row.Runtime?.MatchesPath(e.Key) == true)
            {
                row.RefreshRuntime();
            }
        }
    }

    private void QueueRuntimeRefresh()
    {
        if (Interlocked.Exchange(ref _runtimeRefreshQueued, 1) == 1)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            Interlocked.Exchange(ref _runtimeRefreshQueued, 0);
            foreach (var row in Controllers)
            {
                row.RefreshRuntime();
            }
        }, DispatcherPriority.Background);
    }

    private void RebuildControllers()
    {
        var item = _observedItem;
        Controllers.Clear();
        if (item is null)
        {
            return;
        }

        var runtimeByName = (ViewModel?.GetControllerRuntimes(item, forceRecreate: false) ?? Array.Empty<PidControllerRuntime>())
            .ToDictionary(runtime => runtime.Definition.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var definition in ControllerDefinitionCodec.ParseDefinitions(item.ControllerDefinitions))
        {
            runtimeByName.TryGetValue(definition.Name, out var runtime);
            Controllers.Add(new ControllerRow(item, definition, runtime));
        }

        UpdateFooter(item, Controllers.Count);
    }

    private static void UpdateFooter(FolderItemModel ownerItem, int count)
    {
        ownerItem.Footer = count == 0
            ? "No PID controllers configured"
            : $"{count} PID controller{(count == 1 ? string.Empty : "s")} configured";
    }

    private void ApplyControllerDefinitions(FolderItemModel ownerItem, string rawDefinitions, bool queuePersist)
    {
        Interlocked.Increment(ref _suppressObservedItemRebuild);
        try
        {
            ownerItem.ControllerDefinitions = rawDefinitions;
        }
        finally
        {
            Interlocked.Decrement(ref _suppressObservedItemRebuild);
        }

        RebuildControllers();
        if (!queuePersist || ViewModel is not { } viewModel)
        {
            return;
        }

        if (!viewModel.TrySaveOwningPageYaml(ownerItem, out _))
        {
            viewModel.QueueSaveOwningPageYaml(ownerItem);
        }
    }

    private async void OnAddControllerClicked(object? sender, RoutedEventArgs e)
    {
        var ownerItem = _observedItem;
        var viewModel = ViewModel;
        if (ownerItem is null || viewModel is null || TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var definition = await ControllerEditorDialogWindow.ShowAsync(owner, viewModel, ownerItem, null, GetSourceOptions());
        if (definition is null)
        {
            return;
        }

        var definitions = ControllerDefinitionCodec.ParseDefinitions(ownerItem.ControllerDefinitions).ToList();
        definitions.Add(definition);
        ApplyControllerDefinitions(ownerItem, ControllerDefinitionCodec.SerializeDefinitions(definitions), queuePersist: true);
        e.Handled = true;
    }

    private async void OnEditControllerClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: ControllerRow row })
        {
            return;
        }

        var ownerItem = _observedItem;
        var viewModel = ViewModel;
        if (ownerItem is null || viewModel is null || TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var definitions = ControllerDefinitionCodec.ParseDefinitions(ownerItem.ControllerDefinitions).ToList();
        var currentDefinition = definitions.FirstOrDefault(candidate => string.Equals(candidate.Name, row.Definition.Name, StringComparison.OrdinalIgnoreCase));
        if (currentDefinition is null)
        {
            return;
        }

        var updated = await ControllerEditorDialogWindow.ShowAsync(owner, viewModel, ownerItem, currentDefinition, GetSourceOptions());
        if (updated is null)
        {
            return;
        }

        var index = definitions.FindIndex(candidate => string.Equals(candidate.Name, row.Definition.Name, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return;
        }

        definitions[index] = updated;
        ApplyControllerDefinitions(ownerItem, ControllerDefinitionCodec.SerializeDefinitions(definitions), queuePersist: true);
        e.Handled = true;
    }

    private async void OnDeleteControllerClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: ControllerRow row })
        {
            return;
        }

        var ownerItem = _observedItem;
        if (ownerItem is null || TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var confirmed = await EditorInputDialogs.ConfirmAsync(owner, "Delete controller", $"Delete PID controller '{row.Name}'?");
        if (!confirmed)
        {
            return;
        }

        var definitions = ControllerDefinitionCodec.ParseDefinitions(ownerItem.ControllerDefinitions)
            .Where(definition => !string.Equals(definition.Name, row.Definition.Name, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        ApplyControllerDefinitions(ownerItem, ControllerDefinitionCodec.SerializeDefinitions(definitions), queuePersist: true);
        e.Handled = true;
    }

    private static System.Collections.Generic.IEnumerable<string> GetSourceOptions()
    {
        return MainWindowViewModel.EnumerateSignalSourceOptions();
    }
}

/// <summary>
/// Represents a single controller row in the widget UI.
/// </summary>
public sealed class ControllerRow : ObservableObject
{
    private readonly FolderItemModel _ownerItem;

    /// <summary>
    /// Initializes a new instance of the <see cref="ControllerRow"/> class.
    /// </summary>
    /// <param name="ownerItem">The owning widget item.</param>
    /// <param name="definition">The controller definition.</param>
    public ControllerRow(FolderItemModel ownerItem, ControllerDefinition definition, PidControllerRuntime? runtime)
    {
        _ownerItem = ownerItem;
        Definition = definition.Clone().Normalize();
        Runtime = runtime;
    }

    /// <summary>
    /// Gets the normalized controller definition.
    /// </summary>
    public ControllerDefinition Definition { get; }

    /// <summary>
    /// Gets the runtime when available.
    /// </summary>
    public PidControllerRuntime? Runtime { get; }

    /// <summary>
    /// Gets the controller display name.
    /// </summary>
    public string Name => Definition.Name;

    /// <summary>
    /// Gets the controller type text.
    /// </summary>
    public string TypeText => $"Type: {Definition.Type}";

    /// <summary>
    /// Gets the configured path summary.
    /// </summary>
    public string PathSummary => $"PV: {ValueOrPlaceholder(Definition.SourcePath)} | SET: {GetOwnedSetPath()} | OUT: {ValueOrPlaceholder(Definition.OutputPath)}";

    private string GetOwnedSetPath()
    {
        var folderName = string.IsNullOrWhiteSpace(_ownerItem.FolderName)
            ? _ownerItem.Name
            : _ownerItem.FolderName;
        if (string.IsNullOrWhiteSpace(folderName))
        {
            return "-";
        }

        return PidControllerRuntime.BuildRegistryPath(
            folderName: folderName,
            definition: Definition) + ".set";
    }

    /// <summary>
    /// Gets the tuning and timing summary.
    /// </summary>
    public string ParameterSummary => string.Format(
        CultureInfo.InvariantCulture,
        "Ks {0:0.###} | Tu {1:0.###} | Tg {2:0.###} | compute {3} ms | output {4} ms",
        Definition.Pid.Ks,
        Definition.Pid.Tu,
        Definition.Pid.Tg,
        Definition.Pid.ComputeIntervalMs,
        Definition.Pid.OutputIntervalMs);

    /// <summary>
    /// Gets the runtime state summary.
    /// </summary>
    public string StateText => Runtime is null
        ? "State: pending runtime synchronization"
        : $"State: {Runtime.CurrentStateValue} | Run: {Runtime.IsRunning}";

    /// <summary>
    /// Gets the runtime alert text.
    /// </summary>
    public string AlertText => Runtime?.CurrentAlertValue ?? string.Empty;

    /// <summary>
    /// Gets a value indicating whether an alert is present.
    /// </summary>
    public bool HasAlert => !string.IsNullOrWhiteSpace(AlertText);

    /// <summary>
    /// Gets the row background color.
    /// </summary>
    public string RowBackground => _ownerItem.EffectiveBodyBackground;

    /// <summary>
    /// Gets the row border color.
    /// </summary>
    public string RowBorderBrush => _ownerItem.EffectiveBodyBorder;

    /// <summary>
    /// Gets the primary foreground color.
    /// </summary>
    public string PrimaryForeground => _ownerItem.EffectiveBodyForeground;

    /// <summary>
    /// Gets the secondary foreground color.
    /// </summary>
    public string SecondaryForeground => _ownerItem.EffectiveMutedForeground;

    /// <summary>
    /// Refreshes the row theme bindings.
    /// </summary>
    public void RefreshTheme()
    {
        RaisePropertyChanged(nameof(RowBackground));
        RaisePropertyChanged(nameof(RowBorderBrush));
        RaisePropertyChanged(nameof(PrimaryForeground));
        RaisePropertyChanged(nameof(SecondaryForeground));
    }

    /// <summary>
    /// Refreshes runtime-dependent row state.
    /// </summary>
    public void RefreshRuntime()
    {
        RaisePropertyChanged(nameof(StateText));
        RaisePropertyChanged(nameof(AlertText));
        RaisePropertyChanged(nameof(HasAlert));
    }

    private static string ValueOrPlaceholder(string? value)
        => string.IsNullOrWhiteSpace(value) ? "n/a" : value;
}

public partial class EditorControllerWidget : ControllerControl
{
}
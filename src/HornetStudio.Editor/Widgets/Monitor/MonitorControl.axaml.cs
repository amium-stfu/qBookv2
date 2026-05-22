using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Amium.Items;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using HornetStudio.Editor.Controls;
using HornetStudio.Editor.Helpers;
using HornetStudio.Editor.Models;
using HornetStudio.Editor.ViewModels;
using HornetStudio.Host;
using HornetStudio.Host.Python.Client;
using HornetStudio.Logging;
using Serilog.Events;
using ItemModel = Amium.Items.Item;

namespace HornetStudio.Editor.Widgets;

public partial class MonitorControl : EditorTemplateControl
{
    private static readonly JsonSerializerOptions AggregateMetaJsonOptions = new();

    public static readonly DirectProperty<MonitorControl, bool> HasNoRulesProperty =
        AvaloniaProperty.RegisterDirect<MonitorControl, bool>(nameof(HasNoRules), control => control.HasNoRules);

    private FolderItemModel? _observedItem;
    private bool _hasNoRules = true;
    private int _suppressObservedItemRebuild;

    public MonitorControl()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
        DataContextChanged += OnDataContextChanged;
        Rules.CollectionChanged += OnRulesCollectionChanged;
    }

    public ObservableCollection<MonitorRuleRow> Rules { get; } = [];

    public ObservableCollection<MonitorRuleRow> DisplayRules { get; } = [];

    public bool HasNoRules
    {
        get => _hasNoRules;
        private set => SetAndRaise(HasNoRulesProperty, ref _hasNoRules, value);
    }

    private FolderItemModel? Item => DataContext as FolderItemModel;

    private MainWindowViewModel? ViewModel => TopLevel.GetTopLevel(this)?.DataContext as MainWindowViewModel;

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        HookObservedItem();
        RebuildRules();
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        RemoveAggregateRuntime();
        DisposeRules();
        UnhookObservedItem();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        HookObservedItem();
        RebuildRules();
    }

    private void HookObservedItem()
    {
        var item = Item;
        if (ReferenceEquals(_observedItem, item))
        {
            return;
        }

        UnhookObservedItem();
        if (!IsMonitorItem(item))
        {
            if (item is not null)
            {
                RemoveAggregateRuntime(item);
            }

            return;
        }

        _observedItem = item;
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
            || e.PropertyName == nameof(FolderItemModel.MonitorDefinitions)
            || e.PropertyName == nameof(FolderItemModel.Name)
            || e.PropertyName == nameof(FolderItemModel.Path)
            || e.PropertyName == nameof(FolderItemModel.FolderName))
        {
            if (_suppressObservedItemRebuild == 0)
            {
                RebuildRules();
            }

            return;
        }

        if (e.PropertyName is nameof(FolderItemModel.EffectiveBodyBackground)
            or nameof(FolderItemModel.EffectiveBodyBorder)
            or nameof(FolderItemModel.EffectiveBodyForeground)
            or nameof(FolderItemModel.EffectiveMutedForeground))
        {
            foreach (var row in Rules)
            {
                row.RefreshTheme();
            }
        }
    }

    private void RebuildRules()
    {
        var item = _observedItem;
        DisposeRules();
        Rules.Clear();

        if (item is null)
        {
            UpdateFooter();
            return;
        }

        foreach (var definition in MonitorDefinitionCodec.ParseDefinitions(item.MonitorDefinitions))
        {
            var row = new MonitorRuleRow(item, definition, UpdateFooterAndAggregates);
            Rules.Add(row);
        }

        UpdateFooterAndAggregates();
    }

    private void DisposeRules()
    {
        foreach (var row in Rules)
        {
            row.Dispose();
        }
    }

    private void OnRulesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        HasNoRules = Rules.Count == 0;
        RefreshDisplayRules();
        PublishAggregateRuntime();
    }

    private void RefreshDisplayRules()
    {
        var orderedRules = Rules
            .OrderBy(static row => row.SeveritySortOrder)
            .ThenBy(static row => row.Definition.EventId)
            .ThenBy(static row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        DisplayRules.Clear();
        foreach (var row in orderedRules)
        {
            DisplayRules.Add(row);
        }
    }

    private void UpdateFooter()
    {
        var item = _observedItem;
        if (item is null)
        {
            return;
        }

        var activeCount = Rules.Count(rule => rule.IsActive);
        item.Footer = Rules.Count == 0
            ? "No monitor rules configured"
            : $"{Rules.Count} monitor rule{(Rules.Count == 1 ? string.Empty : "s")}, {activeCount} active";
    }

    private void UpdateFooterAndAggregates()
    {
        UpdateFooter();
        PublishAggregateRuntime();
    }

    private void PublishAggregateRuntime()
    {
        PublishAggregateRuntime(_observedItem, Rules);
    }

    private static bool PublishAggregateRuntime(FolderItemModel? item, IEnumerable<MonitorRuleRow> rules)
    {
        if (item is not { Kind: ControlKind.Monitor })
        {
            return false;
        }

        var runtimePath = MonitorRuleRow.BuildMonitorRegistryPath(item.FolderName, item.Name);
        if (string.IsNullOrWhiteSpace(runtimePath))
        {
            return false;
        }

        var segments = TargetPathHelper.SplitPathSegments(runtimePath);
        if (segments.Count == 0)
        {
            return false;
        }

        var ruleList = rules.ToArray();
        var nameSegment = segments[^1];
        var parentPath = segments.Count > 1 ? string.Join('.', segments.Take(segments.Count - 1)) : string.Empty;
        var snapshot = string.IsNullOrWhiteSpace(parentPath)
            ? new ItemModel(nameSegment, ruleList.Any(rule => rule.IsActive))
            : new ItemModel(nameSegment, ruleList.Any(rule => rule.IsActive), parentPath);

        snapshot.Properties["path"].Value = runtimePath;
        snapshot.Properties["kind"].Value = "MonitorAggregate";
        snapshot.Properties["text"].Value = item.Name;
        snapshot.Properties["title"].Value = item.Name;
        snapshot["active"].Value = ruleList.Any(rule => rule.IsActive);
        snapshot["active"].Properties["text"].Value = "Active";
        snapshot["active_count"].Value = ruleList.Count(rule => rule.IsActive);
        snapshot["active_count"].Properties["text"].Value = "ActiveCount";

        foreach (var aggregate in BuildActiveEventIdAggregates(ruleList))
        {
            snapshot[aggregate.ItemName].Value = aggregate.EventIds;
            snapshot[aggregate.ItemName].Properties["text"].Value = aggregate.ItemName;
            snapshot[aggregate.ItemName].Properties["meta"].Value = aggregate.MetaJson;
        }

        HostRegistries.Data.UpsertSnapshot(runtimePath, snapshot, DataRegistryItemMetadata.WidgetStatus(), pruneMissingMembers: true);
        return true;
    }

    public static IReadOnlyList<MonitorAggregateItem> BuildActiveEventIdAggregates(IEnumerable<MonitorRuleRow> rules)
    {
        var result = new List<MonitorAggregateItem>();
        foreach (var level in Enum.GetValues<MonitorLogLevel>())
        {
            var itemName = $"{TargetPathHelper.NormalizePathSegment(level.ToString(), level.ToString().ToLowerInvariant())}_active";
            var events = rules
                .Where(rule => rule.IsActive && rule.Definition.LogLevel == level)
                .OrderBy(rule => rule.Name, StringComparer.OrdinalIgnoreCase)
                .Select(rule => new MonitorAggregateEvent(rule.Definition.EventId, rule.Definition.EventText ?? string.Empty))
                .ToArray();
            result.Add(new MonitorAggregateItem(
                itemName,
                string.Join(',', events.Select(static entry => entry.EventId.ToString(CultureInfo.InvariantCulture))),
                JsonSerializer.Serialize(new MonitorAggregateMeta(events), AggregateMetaJsonOptions)));
        }

        return result;
    }

    public sealed record MonitorAggregateItem(string ItemName, string EventIds, string MetaJson);

    private sealed record MonitorAggregateMeta([property: JsonPropertyName("events")] IReadOnlyList<MonitorAggregateEvent> Events);

    private sealed record MonitorAggregateEvent(
        [property: JsonPropertyName("event_id")] int EventId,
        [property: JsonPropertyName("text")] string Text);

    private void RemoveAggregateRuntime()
    {
        var item = _observedItem;
        if (item is null)
        {
            return;
        }

        RemoveAggregateRuntime(item);
    }

    private static void RemoveAggregateRuntime(FolderItemModel item)
    {
        HostRegistries.Data.Remove(MonitorRuleRow.BuildMonitorRegistryPath(item.FolderName, item.Name));
    }

    private static bool IsMonitorItem(FolderItemModel? item)
        => item?.Kind == ControlKind.Monitor;

    private void ApplyDefinitions(FolderItemModel ownerItem, IReadOnlyList<MonitorDefinition> definitions, bool queuePersist)
    {
        _suppressObservedItemRebuild++;
        try
        {
            ownerItem.MonitorDefinitions = MonitorDefinitionCodec.SerializeDefinitions(definitions);
        }
        finally
        {
            _suppressObservedItemRebuild--;
        }

        RebuildRules();
        if (queuePersist && ViewModel is { } viewModel)
        {
            if (!viewModel.TrySaveOwningPageYaml(ownerItem, out _))
            {
                viewModel.QueueSaveOwningPageYaml(ownerItem);
            }
        }
    }

    private async void OnAddRuleClicked(object? sender, RoutedEventArgs e)
    {
        var ownerItem = _observedItem;
        if (ownerItem is null || TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var definition = await MonitorEditorDialogWindow.ShowAsync(owner, ViewModel, ownerItem, null, MainWindowViewModel.EnumerateSignalSourceOptions(), GetProcessLogTargetOptions());
        if (definition is null)
        {
            return;
        }

        var definitions = MonitorDefinitionCodec.ParseDefinitions(ownerItem.MonitorDefinitions).ToList();
        definitions.Add(definition);
        ApplyDefinitions(ownerItem, definitions, queuePersist: true);
        e.Handled = true;
    }

    private async void OnEditRuleClicked(object? sender, RoutedEventArgs e)
    {
        if (!TryGetRuleRowFromActionSource(sender, out var row))
        {
            return;
        }

        var ownerItem = _observedItem;
        if (ownerItem is null || TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var definitions = MonitorDefinitionCodec.ParseDefinitions(ownerItem.MonitorDefinitions).ToList();
        var index = definitions.FindIndex(candidate => string.Equals(candidate.Name, row.Name, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return;
        }

        var updated = await MonitorEditorDialogWindow.ShowAsync(owner, ViewModel, ownerItem, definitions[index], MainWindowViewModel.EnumerateSignalSourceOptions(), GetProcessLogTargetOptions());
        if (updated is null)
        {
            return;
        }

        definitions[index] = updated;
        ApplyDefinitions(ownerItem, definitions, queuePersist: true);
        e.Handled = true;
    }

    private async void OnDeleteRuleClicked(object? sender, RoutedEventArgs e)
    {
        if (!TryGetRuleRowFromActionSource(sender, out var row))
        {
            return;
        }

        var ownerItem = _observedItem;
        if (ownerItem is null || TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var confirmed = await EditorInputDialogs.ConfirmAsync(owner, $"Delete monitor rule '{row.Name}'?", "The monitor rule definition will be removed.", confirmText: "Delete", cancelText: "Cancel");
        if (!confirmed)
        {
            return;
        }

        var definitions = MonitorDefinitionCodec.ParseDefinitions(ownerItem.MonitorDefinitions)
            .Where(definition => !string.Equals(definition.Name, row.Name, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        ApplyDefinitions(ownerItem, definitions, queuePersist: true);
        e.Handled = true;
    }

    private void OnRuleActionsClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.ContextMenu is null)
        {
            return;
        }

        button.ContextMenu.DataContext = button.Tag ?? button.DataContext;
        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.Open();
        e.Handled = true;
    }

    private static bool TryGetRuleRowFromActionSource(object? source, out MonitorRuleRow row)
    {
        row = source switch
        {
            Button { CommandParameter: MonitorRuleRow commandRow } => commandRow,
            Button { Tag: MonitorRuleRow tagRow } => tagRow,
            Button { DataContext: MonitorRuleRow contextRow } => contextRow,
            MenuItem { CommandParameter: MonitorRuleRow commandRow } => commandRow,
            MenuItem { Tag: MonitorRuleRow tagRow } => tagRow,
            MenuItem { DataContext: MonitorRuleRow contextRow } => contextRow,
            _ => null!
        };

        return row is not null;
    }

    private static IEnumerable<string> GetProcessLogTargetOptions()
    {
        return HostRegistries.Data.GetKeysByCapability(DataRegistryItemCapabilities.Display)
            .Select(key => HostRegistries.Data.TryGet(key, out var item) ? (Key: key, Item: item) : (Key: (string?)null, Item: null))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Key) && entry.Item?.Value is ProcessLog)
            .Select(entry => entry.Key!)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

public sealed class MonitorRuleRow : ObservableObject, IDisposable
{
    private readonly FolderItemModel _ownerItem;
    private readonly string _runtimePath;
    private readonly Action _stateChanged;
    private readonly DispatcherTimer _evaluationTimer;
    private DateTimeOffset? _conditionStartedUtc;
    private bool _isActive;
    private string _statusText = "Inactive";

    public MonitorRuleRow(FolderItemModel ownerItem, MonitorDefinition definition, Action stateChanged)
    {
        _ownerItem = ownerItem;
        Definition = definition.Clone();
        _stateChanged = stateChanged;
        _runtimePath = BuildRegistryPath(ownerItem.FolderName, ownerItem.Name, definition.Name);
        _evaluationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(Math.Max(250, Definition.RefreshRateMs))
        };
        _evaluationTimer.Tick += OnEvaluationTimerTick;
        _evaluationTimer.Start();
        Evaluate();
    }

    public MonitorDefinition Definition { get; }

    public string Name => Definition.Name;

    public bool IsActive => _isActive;

    public string EventIdText => Definition.EventId.ToString(CultureInfo.InvariantCulture);

    public string EventDisplayText => string.IsNullOrWhiteSpace(Definition.EventText) ? Name : Definition.EventText.Trim();

    public int SeveritySortOrder => GetSeveritySortOrder(Definition.LogLevel);

    public string SourceText => $"Source: {ValueOrPlaceholder(Definition.SourcePath)} | Mode: {Definition.Mode} | Every {Definition.RefreshRateMs} ms";

    public string StatusText => _statusText;

    public string RowTooltip => $"{SourceText}{Environment.NewLine}{StatusText}";

    public string ActionTooltip => $"Rule actions: {Name}";

    public string RowBackground => _isActive
        ? GetActiveRowBackground()
        : _ownerItem.EffectiveBodyBackground;

    public string RowBorderBrush => _isActive
        ? GetSeverityForeground()
        : _ownerItem.EffectiveBodyBorder;

    public string PrimaryForeground => _ownerItem.EffectiveBodyForeground;

    public string SecondaryForeground => _ownerItem.EffectiveMutedForeground;

    public void RefreshTheme()
    {
        RaisePropertyChanged(nameof(RowBackground));
        RaisePropertyChanged(nameof(RowBorderBrush));
        RaisePropertyChanged(nameof(PrimaryForeground));
        RaisePropertyChanged(nameof(SecondaryForeground));
        RaisePropertyChanged(nameof(ActionTooltip));
    }

    private void OnEvaluationTimerTick(object? sender, EventArgs e)
    {
        Evaluate();
    }

    public void Evaluate()
    {
        var evaluation = EvaluateState();
        var wasActive = _isActive;
        _isActive = evaluation.IsActive;
        _statusText = evaluation.StatusText;

        PublishRuntime(evaluation);
        ExecuteTransitionActions(wasActive, evaluation);

        RaisePropertyChanged(nameof(IsActive));
        RaisePropertyChanged(nameof(StatusText));
        RaisePropertyChanged(nameof(RowTooltip));
        RaisePropertyChanged(nameof(RowBackground));
        RaisePropertyChanged(nameof(RowBorderBrush));
        _stateChanged();
    }

    private string GetActiveRowBackground()
    {
        var backgroundText = GetRowBlendBaseBackground();
        return TryBlendColors(
            backgroundText: backgroundText,
            accentText: GetSeverityForeground(),
            blendFactor: IsDarkColor(backgroundText) ? 0.26 : 0.32,
            blendedColorText: out var blendedColorText)
            ? blendedColorText
            : _ownerItem.EffectiveBodyBackground;
    }

    private string GetSeverityForeground()
    {
        var theme = IsDarkColor(GetRowBlendBaseBackground()) ? ThemePalette.Dark : ThemePalette.Light;
        return Definition.LogLevel switch
        {
            MonitorLogLevel.Debug => theme.LogDebugForeground,
            MonitorLogLevel.Info => theme.LogInfoForeground,
            MonitorLogLevel.Warning => theme.LogWarningForeground,
            MonitorLogLevel.Error => theme.LogErrorForeground,
            MonitorLogLevel.Fatal => theme.LogFatalForeground,
            _ => _ownerItem.EffectiveMutedForeground
        };
    }

    private string GetRowBlendBaseBackground()
    {
        if (TryGetOpaqueColorText(_ownerItem.EffectiveBodyBackground, out var bodyBackground))
        {
            return bodyBackground;
        }

        if (TryGetOpaqueColorText(_ownerItem.EffectiveInnerBackground, out var innerBackground))
        {
            return innerBackground;
        }

        return IsDarkColor(_ownerItem.EffectiveBackground)
            ? ThemePalette.Dark.CardBackground
            : ThemePalette.Light.CardBackground;
    }

    private static bool TryGetOpaqueColorText(string? colorText, out string opaqueColorText)
    {
        opaqueColorText = string.Empty;
        if (string.IsNullOrWhiteSpace(colorText)
            || !Color.TryParse(colorText, out var color)
            || color.A == byte.MinValue)
        {
            return false;
        }

        opaqueColorText = Color.FromArgb(byte.MaxValue, color.R, color.G, color.B).ToString();
        return true;
    }

    private static bool TryBlendColors(string? backgroundText, string? accentText, double blendFactor, out string blendedColorText)
    {
        blendedColorText = backgroundText ?? string.Empty;
        if (string.IsNullOrWhiteSpace(backgroundText)
            || string.IsNullOrWhiteSpace(accentText)
            || !Color.TryParse(backgroundText, out var background)
            || !Color.TryParse(accentText, out var accent))
        {
            return false;
        }

        var clampedBlendFactor = Math.Clamp(blendFactor, 0d, 1d);
        var blended = Color.FromArgb(
            byte.MaxValue,
            BlendChannel(background.R, accent.R, clampedBlendFactor),
            BlendChannel(background.G, accent.G, clampedBlendFactor),
            BlendChannel(background.B, accent.B, clampedBlendFactor));

        blendedColorText = blended.ToString();
        return true;
    }

    private static byte BlendChannel(byte backgroundChannel, byte accentChannel, double blendFactor)
    {
        var blended = backgroundChannel + ((accentChannel - backgroundChannel) * blendFactor);
        return (byte)Math.Clamp((int)Math.Round(blended, MidpointRounding.AwayFromZero), byte.MinValue, byte.MaxValue);
    }

    private static int GetSeveritySortOrder(MonitorLogLevel level)
    {
        return level switch
        {
            MonitorLogLevel.Fatal => 0,
            MonitorLogLevel.Error => 1,
            MonitorLogLevel.Warning => 2,
            MonitorLogLevel.Info => 3,
            MonitorLogLevel.Debug => 4,
            _ => int.MaxValue
        };
    }

    private static bool IsDarkColor(string? colorText)
    {
        if (string.IsNullOrWhiteSpace(colorText) || !Color.TryParse(colorText, out var color))
        {
            return false;
        }

        var brightness = (0.299 * color.R) + (0.587 * color.G) + (0.114 * color.B);
        return brightness < 140;
    }

    public void Dispose()
    {
        _evaluationTimer.Stop();
        _evaluationTimer.Tick -= OnEvaluationTimerTick;
        if (!string.IsNullOrWhiteSpace(_runtimePath))
        {
            HostRegistries.Data.Remove(_runtimePath);
        }
    }

    private MonitorEvaluation EvaluateState()
    {
        var activationReasons = new List<string>();
        var notes = new List<string>();
        var now = DateTimeOffset.UtcNow;
        TryResolveSourceItem(Definition.SourcePath, _ownerItem.FolderName, out var sourceItem);

        if (Definition.TimeoutMs.HasValue && Definition.TimeoutMs.Value > 0)
        {
            if (sourceItem is null || !TryReadItemEpoch(sourceItem, out var epoch))
            {
                activationReasons.Add($"Timeout > {Definition.TimeoutMs.Value} ms (epoch unavailable)");
            }
            else
            {
                var ageMs = Math.Max(0, now.ToUnixTimeMilliseconds() - (long)epoch);
                if (ageMs > Definition.TimeoutMs.Value)
                {
                    activationReasons.Add($"Timeout > {Definition.TimeoutMs.Value} ms");
                }
            }
        }

        object? value = sourceItem?.Value;
        if (Definition.Mode == MonitorRuleMode.Default)
        {
            EvaluateNumericLimit(Definition.LowerLimit, value, static (current, limit) => current < limit, "Lower limit", activationReasons, notes);
            EvaluateNumericLimit(Definition.UpperLimit, value, static (current, limit) => current > limit, "Upper limit", activationReasons, notes);
        }
        else
        {
            EvaluateCustomFormula(value, activationReasons, notes);
        }

        var rawActive = activationReasons.Count > 0;
        if (rawActive)
        {
            _conditionStartedUtc ??= now;
        }
        else
        {
            _conditionStartedUtc = null;
        }

        var effectiveInhibitMs = Math.Max(0, Definition.InhibitMs);
        var isActive = rawActive && (_conditionStartedUtc is not null) && (now - _conditionStartedUtc.Value).TotalMilliseconds >= effectiveInhibitMs;
        var statusText = BuildStatusText(rawActive, isActive, activationReasons, notes, effectiveInhibitMs, now);

        return new MonitorEvaluation(isActive, statusText, value);
    }

    private void EvaluateCustomFormula(object? sourceValue, List<string> activationReasons, List<string> notes)
    {
        if (string.IsNullOrWhiteSpace(Definition.CustomFormula))
        {
            notes.Add("Formula missing");
            return;
        }

        var variables = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["value"] = sourceValue,
            ["source"] = sourceValue
        };

        foreach (var variable in Definition.CustomVariables)
        {
            if (string.IsNullOrWhiteSpace(variable.Name))
            {
                continue;
            }

            if (TryResolveSourceItem(variable.SourcePath, _ownerItem.FolderName, out var variableItem) && variableItem is not null)
            {
                variables[variable.Name] = variableItem.Value;
            }
            else
            {
                variables[variable.Name] = null;
            }
        }

        if (!CustomSignalFormulaEngine.TryEvaluateBooleanExpression(Definition.CustomFormula, variables, out var expressionActive, out var errorMessage))
        {
            notes.Add($"Formula invalid: {errorMessage}");
            return;
        }

        if (expressionActive)
        {
            activationReasons.Add("Formula matched");
        }
    }

    private void PublishRuntime(MonitorEvaluation evaluation)
    {
        if (string.IsNullOrWhiteSpace(_runtimePath))
        {
            return;
        }

        var segments = TargetPathHelper.SplitPathSegments(_runtimePath);
        if (segments.Count == 0)
        {
            return;
        }

        var nameSegment = segments[^1];
        var parentPath = segments.Count > 1 ? string.Join('.', segments.Take(segments.Count - 1)) : string.Empty;
        var snapshot = string.IsNullOrWhiteSpace(parentPath)
            ? new ItemModel(nameSegment, evaluation.IsActive)
            : new ItemModel(nameSegment, evaluation.IsActive, parentPath);

        var title = string.IsNullOrWhiteSpace(Definition.EventText) ? Definition.Name : Definition.EventText;
        snapshot.Properties["path"].Value = _runtimePath;
        snapshot.Properties["kind"].Value = "MonitorState";
        snapshot.Properties["text"].Value = title;
        snapshot.Properties["title"].Value = title;
        snapshot["active"].Value = evaluation.IsActive;
        snapshot["active"].Properties["text"].Value = "Active";
        snapshot["message"].Value = evaluation.StatusText;
        snapshot["message"].Properties["text"].Value = "Message";
        snapshot["event_id"].Value = Definition.EventId;
        snapshot["event_id"].Properties["text"].Value = "EventId";
        snapshot["event_text"].Value = Definition.EventText;
        snapshot["event_text"].Properties["text"].Value = "EventText";
        snapshot["log_level"].Value = Definition.LogLevel.ToString();
        snapshot["log_level"].Properties["text"].Value = "LogLevel";
        snapshot["source_path"].Value = Definition.SourcePath;
        snapshot["source_path"].Properties["text"].Value = "SourcePath";
        snapshot["mode"].Value = Definition.Mode.ToString();
        snapshot["mode"].Properties["text"].Value = "Mode";
        snapshot["refresh_rate_ms"].Value = Definition.RefreshRateMs;
        snapshot["refresh_rate_ms"].Properties["text"].Value = "RefreshRateMs";
        snapshot["action_count"].Value = Definition.Actions.Count;
        snapshot["action_count"].Properties["text"].Value = "ActionCount";
        HostRegistries.Data.UpsertSnapshot(_runtimePath, snapshot, DataRegistryItemMetadata.WidgetStatus(), pruneMissingMembers: true);
    }

    private void ExecuteTransitionActions(bool wasActive, MonitorEvaluation evaluation)
    {
        if (!wasActive && evaluation.IsActive)
        {
            ExecuteActions(MonitorActionTrigger.OnActivated, evaluation);
            return;
        }

        if (wasActive && !evaluation.IsActive)
        {
            ExecuteActions(MonitorActionTrigger.OnCleared, evaluation);
        }
    }

    private void ExecuteActions(MonitorActionTrigger trigger, MonitorEvaluation evaluation)
    {
        foreach (var action in Definition.Actions.Where(action => action.Trigger == trigger))
        {
            Core.LogInfo(
                $"[MonitorAction] trigger={trigger} rule={Definition.Name} action={action.ActionType} active={evaluation.IsActive} target_log={action.TargetLog} target_path={action.TargetPath} function={action.FunctionName} argument={action.Argument}");

            switch (action.ActionType)
            {
                case MonitorActionType.WriteLog:
                    WriteProcessLog(action, evaluation);
                    break;
                case MonitorActionType.SetValue:
                    ExecuteSetValue(action);
                    break;
                case MonitorActionType.InvokeFunction:
                    ExecuteInvokeFunction(action);
                    break;
            }
        }
    }

    private void ExecuteSetValue(MonitorActionDefinition action)
    {
        if (string.IsNullOrWhiteSpace(action.TargetPath))
        {
            return;
        }

        if (!TryResolveActionTarget(action.TargetPath, out var targetItem) || targetItem is null)
        {
            Core.LogWarn($"[Monitor] SetValue target '{action.TargetPath}' for rule '{Definition.Name}' could not be resolved.");
            return;
        }

        if (!TryApplyActionWrite(targetItem, action.TargetPath, action.Argument, out var errorMessage))
        {
            Core.LogWarn($"[Monitor] SetValue target '{action.TargetPath}' for rule '{Definition.Name}' failed: {errorMessage}");
            return;
        }

        Core.LogInfo($"[MonitorAction] SetValue applied rule={Definition.Name} target={action.TargetPath} argument={action.Argument}");
    }

    private void ExecuteInvokeFunction(MonitorActionDefinition action)
    {
        if (string.IsNullOrWhiteSpace(action.TargetPath) || string.IsNullOrWhiteSpace(action.FunctionName))
        {
            return;
        }

        var resolvedTargetPath = ApplicationExplorerRuntime.ResolveInteractionTargetPath(_ownerItem, action.TargetPath);
        if (!PythonClientRuntimeRegistry.TryGetClient(resolvedTargetPath, out var client) || client is null)
        {
            Core.LogWarn($"[Monitor] InvokeFunction target '{action.TargetPath}' for rule '{Definition.Name}' is not active.");
            return;
        }

        try
        {
            var result = client.InvokeFunctionAsync(action.FunctionName, BuildPythonArgumentPayload(action.Argument))
                .GetAwaiter()
                .GetResult();

            if (!result.Success)
            {
                var errorMessage = string.IsNullOrWhiteSpace(result.Message)
                    ? $"Function '{action.FunctionName}' failed."
                    : result.Message!;
                if (ApplicationEntryRegistry.TryGetByInteractionTargetPath(resolvedTargetPath, out var failedRow))
                {
                    failedRow?.SetInvocationError(ApplicationErrorDetails.FromResultPayload(failedRow.Name, errorMessage, result.Payload));
                }

                Core.LogWarn($"[Monitor] InvokeFunction '{action.FunctionName}' in '{resolvedTargetPath}' for rule '{Definition.Name}' failed: {errorMessage}");
                return;
            }

            if (ApplicationEntryRegistry.TryGetByInteractionTargetPath(resolvedTargetPath, out var successRow))
            {
                successRow?.ClearInvocationError();
            }
        }
        catch (Exception ex)
        {
            if (ApplicationEntryRegistry.TryGetByInteractionTargetPath(resolvedTargetPath, out var failedRow))
            {
                failedRow?.SetInvocationError(ex.Message);
            }

            Core.LogWarn($"[Monitor] InvokeFunction '{action.FunctionName}' in '{resolvedTargetPath}' for rule '{Definition.Name}' threw an exception: {ex.Message}", ex);
        }
    }

    private void WriteProcessLog(MonitorActionDefinition action, MonitorEvaluation evaluation)
    {
        if (string.IsNullOrWhiteSpace(action.TargetLog))
        {
            return;
        }

        if (!TryResolveProcessLog(action.TargetLog, _ownerItem.FolderName, out var processLog) || processLog is null)
        {
            Core.LogWarn($"[Monitor] WriteLog target '{action.TargetLog}' for rule '{Definition.Name}' could not be resolved.");
            return;
        }

        var message = string.IsNullOrWhiteSpace(Definition.EventText)
            ? evaluation.StatusText
            : $"[{Definition.EventId}] {Definition.EventText}";
        processLog.WriteEntry(ToLogEventLevel(Definition.LogLevel), message);
    }

    private string BuildStatusText(bool rawActive, bool isActive, IReadOnlyList<string> activationReasons, IReadOnlyList<string> notes, int inhibitMs, DateTimeOffset now)
    {
        if (!rawActive)
        {
            return notes.Count == 0 ? "Inactive" : $"Inactive: {string.Join(" | ", notes)}";
        }

        if (!isActive)
        {
            var elapsedMs = _conditionStartedUtc.HasValue ? Math.Max(0, (int)(now - _conditionStartedUtc.Value).TotalMilliseconds) : 0;
            var remainingMs = Math.Max(0, inhibitMs - elapsedMs);
            var messages = activationReasons.Concat(notes).ToArray();
            return messages.Length == 0
                ? $"Inhibit active ({remainingMs} ms remaining)"
                : $"Inhibit active ({remainingMs} ms remaining): {string.Join(" | ", messages)}";
        }

        var activeMessages = activationReasons.Concat(notes).ToArray();
        return activeMessages.Length == 0 ? "Active" : $"Active: {string.Join(" | ", activeMessages)}";
    }

    private static void EvaluateNumericLimit(string rawConfigured, object? value, Func<double, double, bool> compare, string label, List<string> activationReasons, List<string> notes)
    {
        if (string.IsNullOrWhiteSpace(rawConfigured))
        {
            return;
        }

        if (!double.TryParse(rawConfigured, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var limit))
        {
            notes.Add($"{label} invalid");
            return;
        }

        if (!TryConvertToDouble(value, out var numericValue))
        {
            notes.Add($"{label} skipped (source not numeric)");
            return;
        }

        if (compare(numericValue, limit))
        {
            activationReasons.Add($"{label} {limit.ToString(CultureInfo.InvariantCulture)}");
        }
    }

    private static bool TryConvertToDouble(object? value, out double numericValue)
    {
        switch (value)
        {
            case byte byteValue:
                numericValue = byteValue;
                return true;
            case sbyte signedByteValue:
                numericValue = signedByteValue;
                return true;
            case short shortValue:
                numericValue = shortValue;
                return true;
            case ushort unsignedShortValue:
                numericValue = unsignedShortValue;
                return true;
            case int intValue:
                numericValue = intValue;
                return true;
            case uint unsignedIntValue:
                numericValue = unsignedIntValue;
                return true;
            case long longValue:
                numericValue = longValue;
                return true;
            case ulong unsignedLongValue:
                numericValue = unsignedLongValue;
                return true;
            case float floatValue:
                numericValue = floatValue;
                return true;
            case double doubleValue:
                numericValue = doubleValue;
                return true;
            case decimal decimalValue:
                numericValue = (double)decimalValue;
                return true;
            case string text when double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed):
                numericValue = parsed;
                return true;
            default:
                numericValue = 0;
                return false;
        }
    }

    private static bool TryResolveSourceItem(string targetPath, string? folderName, out ItemModel? item)
    {
        foreach (var candidatePath in TargetPathHelper.EnumerateResolutionCandidates(targetPath, folderName))
        {
            if (HostRegistries.Data.TryResolve(candidatePath, out item) && item is not null)
            {
                return true;
            }
        }

        foreach (var candidatePath in TargetPathHelper.EnumerateItemBrokerRuntimeCandidates(targetPath))
        {
            if (HostRegistries.Data.TryResolve(candidatePath, out item) && item is not null)
            {
                return true;
            }
        }

        item = null;
        return false;
    }

    private bool TryResolveActionTarget(string? targetPath, out ItemModel? item)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            item = null;
            return false;
        }

        return TryResolveSourceItem(targetPath, _ownerItem.FolderName, out item);
    }

    private static bool TryApplyActionWrite(ItemModel targetItem, string? targetPath, object? rawValue, out string error)
    {
        if (!IsDeclaredWritable(targetItem))
        {
            error = "Target is not writable.";
            return false;
        }

        var writeParameter = ResolveActionWriteParameter(targetItem);
        var readParameter = ResolveActionReadParameter(targetItem);
        var writeTargetItem = ResolveActionWriteTargetItem(targetItem);
        if (writeParameter is null)
        {
            error = "No write parameter was found for the action target.";
            return false;
        }

        try
        {
            var convertedValue = ConvertActionValue(rawValue, writeParameter.Value?.GetType() ?? readParameter?.Value?.GetType());
            if (!string.Equals(writeParameter.Name, "read", StringComparison.OrdinalIgnoreCase)
                && !HostRegistryPropertyPolicy.CanUserWriteProperty(writeParameter.Name))
            {
                error = $"Parameter '{writeParameter.Name}' is protected and cannot be written.";
                return false;
            }

            var resolvedTargetPath = writeTargetItem.Path ?? targetItem.Path ?? targetPath ?? string.Empty;
            var forceWriteNotification = string.Equals(writeParameter.Name, "write", StringComparison.OrdinalIgnoreCase);
            var updated = string.Equals(writeParameter.Name, "read", StringComparison.OrdinalIgnoreCase)
                ? HostRegistries.Data.UpdateValue(resolvedTargetPath, convertedValue)
                : HostRegistries.Data.TryUpdateUserProperty(resolvedTargetPath, writeParameter.Name, convertedValue, forceChangeNotification: forceWriteNotification);
            if (!updated)
            {
                if (string.Equals(writeParameter.Name, "read", StringComparison.OrdinalIgnoreCase))
                {
                    writeTargetItem.Value = convertedValue!;
                }
                else
                {
                    writeParameter.Value = convertedValue!;
                }

                PublishActionSnapshot(targetItem);
            }

            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static ItemProperty? ResolveActionReadParameter(ItemModel targetItem)
    {
        if (targetItem.Properties.Has("read"))
        {
            return targetItem.Properties["read"];
        }

        var firstParameter = targetItem.Properties.GetDictionary().Keys
            .Where(HostRegistryPropertyPolicy.CanShowInUserPicker)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        return firstParameter is null ? null : targetItem.Properties[firstParameter];
    }

    private static ItemProperty? ResolveActionWriteParameter(ItemModel targetItem)
    {
        if (targetItem.Properties.Has("write"))
        {
            return targetItem.Properties["write"];
        }

        if (TryResolveDeclaredWriteBinding(targetItem, out var declaredTarget))
        {
            return declaredTarget.Properties.Has("write")
                ? declaredTarget.Properties["write"]
                : ResolveValueParameter(declaredTarget);
        }

        return ResolveActionReadParameter(targetItem);
    }

    private static ItemModel ResolveActionWriteTargetItem(ItemModel targetItem)
        => TryResolveDeclaredWriteBinding(targetItem, out var declaredTarget) ? declaredTarget : targetItem;

    private static bool IsDeclaredWritable(ItemModel? item)
    {
        if (item is null)
        {
            return false;
        }

        if (item.Properties.Has("write"))
        {
            return true;
        }

        if (item.Properties.Has("writable"))
        {
            return ToBooleanLikeValue(item.Properties["writable"].Value);
        }

        return true;
    }

    private static ItemProperty? ResolveValueParameter(ItemModel item)
        => item.Properties.Has("read") ? item.Properties["read"] : null;

    private static bool TryResolveDeclaredWriteBinding(ItemModel sourceItem, out ItemModel writeTargetItem)
    {
        writeTargetItem = null!;
        if (sourceItem.Properties.Has("write"))
        {
            writeTargetItem = sourceItem;
            return true;
        }

        if (!sourceItem.Properties.Has("write_path"))
        {
            return false;
        }

        var writePath = sourceItem.Properties["write_path"].Value?.ToString()?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(writePath))
        {
            return false;
        }

        if (!HostRegistries.Data.TryResolve(writePath, out ItemModel? resolvedItem) || resolvedItem is null)
        {
            return false;
        }

        writeTargetItem = resolvedItem!;
        return true;
    }

    private static object? ConvertActionValue(object? rawValue, Type? targetType)
    {
        if (targetType is null || rawValue is null)
        {
            return rawValue;
        }

        var effectiveType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (effectiveType.IsInstanceOfType(rawValue))
        {
            return rawValue;
        }

        if (effectiveType.IsEnum)
        {
            return rawValue switch
            {
                string text when Enum.TryParse(effectiveType, text, ignoreCase: true, out var parsedEnum) => parsedEnum,
                _ => TryConvertEnumNumeric(rawValue, effectiveType)
            };
        }

        if (rawValue is string textValue)
        {
            if (effectiveType == typeof(string))
            {
                return textValue;
            }

            if (effectiveType == typeof(bool))
            {
                if (bool.TryParse(textValue, out var boolResult))
                {
                    return boolResult;
                }

                if (long.TryParse(textValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericBool))
                {
                    return numericBool != 0;
                }

                return rawValue;
            }

            if (effectiveType == typeof(byte))
            {
                return byte.TryParse(textValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : rawValue;
            }

            if (effectiveType == typeof(sbyte))
            {
                return sbyte.TryParse(textValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : rawValue;
            }

            if (effectiveType == typeof(short))
            {
                return short.TryParse(textValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : rawValue;
            }

            if (effectiveType == typeof(ushort))
            {
                return ushort.TryParse(textValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : rawValue;
            }

            if (effectiveType == typeof(int))
            {
                return int.TryParse(textValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : rawValue;
            }

            if (effectiveType == typeof(uint))
            {
                return uint.TryParse(textValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : rawValue;
            }

            if (effectiveType == typeof(long))
            {
                return long.TryParse(textValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : rawValue;
            }

            if (effectiveType == typeof(ulong))
            {
                return ulong.TryParse(textValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : rawValue;
            }

            if (effectiveType == typeof(float))
            {
                return float.TryParse(textValue, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed) ? parsed : rawValue;
            }

            if (effectiveType == typeof(double))
            {
                return double.TryParse(textValue, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed) ? parsed : rawValue;
            }

            if (effectiveType == typeof(decimal))
            {
                return decimal.TryParse(textValue, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed) ? parsed : rawValue;
            }
        }

        try
        {
            return Convert.ChangeType(rawValue, effectiveType, CultureInfo.InvariantCulture);
        }
        catch
        {
            return rawValue;
        }
    }

    private static object? TryConvertEnumNumeric(object rawValue, Type enumType)
    {
        try
        {
            return Enum.ToObject(enumType, Convert.ToInt64(rawValue, CultureInfo.InvariantCulture));
        }
        catch
        {
            return rawValue;
        }
    }

    private static void PublishActionSnapshot(ItemModel item)
    {
        if (string.IsNullOrWhiteSpace(item.Path))
        {
            return;
        }

        HostRegistries.Data.UpsertSnapshot(item.Path!, item.Clone(), DataRegistryItemMetadata.PublicData(), pruneMissingMembers: true);
    }

    private static JsonNode BuildPythonArgumentPayload(string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            return new JsonObject();
        }

        var trimmed = argument.Trim();
        try
        {
            var parsed = JsonNode.Parse(trimmed);
            if (parsed is JsonObject or JsonArray)
            {
                return parsed;
            }

            return new JsonObject
            {
                ["value"] = parsed
            };
        }
        catch
        {
            return new JsonObject
            {
                ["value"] = trimmed
            };
        }
    }

    private static bool ToBooleanLikeValue(object? value)
        => value switch
        {
            bool boolValue => boolValue,
            string text when bool.TryParse(text, out var parsedBool) => parsedBool,
            string text when long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedLong) => parsedLong != 0,
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

    private static bool TryReadItemEpoch(ItemModel item, out ulong epoch)
    {
        if (item.Properties.Has("epoch"))
        {
            var value = item.Properties["epoch"].Value;
            if (value is ulong ulongValue)
            {
                epoch = ulongValue;
                return true;
            }

            if (ulong.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out ulong parsed))
            {
                epoch = parsed;
                return true;
            }
        }

        epoch = 0;
        return false;
    }

    private static bool TryResolveProcessLog(string? targetLog, string? folderName, out ProcessLog? resolved)
    {
        resolved = null;
        if (string.IsNullOrWhiteSpace(targetLog))
        {
            return false;
        }

        var normalized = NormalizeLogTargetPath(targetLog);
        foreach (var candidate in EnumerateProcessLogResolutionCandidates(normalized, folderName))
        {
            if (HostRegistries.Data.TryResolve(candidate, out var item) && item?.Value is ProcessLog processLog)
            {
                resolved = processLog;
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> EnumerateProcessLogResolutionCandidates(string normalizedTargetLog, string? folderName)
    {
        if (string.IsNullOrWhiteSpace(normalizedTargetLog))
        {
            yield break;
        }

        yield return normalizedTargetLog;

        var normalizedFolder = TargetPathHelper.NormalizeConfiguredTargetPath(folderName);
        if (string.IsNullOrWhiteSpace(normalizedFolder))
        {
            yield break;
        }

        if (normalizedTargetLog.StartsWith("logs.", StringComparison.OrdinalIgnoreCase))
        {
            yield return $"studio.{normalizedFolder}.{normalizedTargetLog}";
        }
    }

    private static string NormalizeLogTargetPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = TargetPathHelper.NormalizeConfiguredTargetPath(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        return normalized.Contains('.', StringComparison.Ordinal)
            ? normalized
            : $"Logs.{normalized}";
    }

    private static LogEventLevel ToLogEventLevel(MonitorLogLevel level)
    {
        return level switch
        {
            MonitorLogLevel.Debug => LogEventLevel.Debug,
            MonitorLogLevel.Info => LogEventLevel.Information,
            MonitorLogLevel.Warning => LogEventLevel.Warning,
            MonitorLogLevel.Error => LogEventLevel.Error,
            MonitorLogLevel.Fatal => LogEventLevel.Fatal,
            _ => LogEventLevel.Warning
        };
    }

    public static string BuildRegistryPath(string? folderName, string? ownerName, string? ruleName)
    {
        var normalizedFolder = TargetPathHelper.NormalizePathSegment(folderName, "page");
        var normalizedOwner = TargetPathHelper.NormalizePathSegment(ownerName, "monitor");
        var normalizedRule = TargetPathHelper.NormalizePathSegment(ruleName, "rule");
        return $"studio.{normalizedFolder}.monitor.{normalizedOwner}.{normalizedRule}";
    }

    public static string BuildMonitorRegistryPath(string? folderName, string? ownerName)
    {
        var normalizedFolder = TargetPathHelper.NormalizePathSegment(folderName, "page");
        var normalizedOwner = TargetPathHelper.NormalizePathSegment(ownerName, "monitor");
        return $"studio.{normalizedFolder}.monitor.{normalizedOwner}";
    }

    private static string ValueOrPlaceholder(string? value)
        => string.IsNullOrWhiteSpace(value) ? "n/a" : value;

    private sealed record MonitorEvaluation(bool IsActive, string StatusText, object? Value);
}

public partial class EditorMonitorWidget : MonitorControl
{
}

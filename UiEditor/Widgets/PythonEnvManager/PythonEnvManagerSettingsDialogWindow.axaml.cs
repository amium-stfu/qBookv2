using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Amium.Host;
using Amium.UiEditor.Models;
using Amium.UiEditor.ViewModels;

namespace Amium.UiEditor.Widgets;

public partial class PythonEnvManagerSettingsDialogWindow : UserControl, INotifyPropertyChanged
{
    private EditorDialogField? _field;
    private MainWindowViewModel? _viewModel;
    private MainWindowViewModel? _subscribedViewModel;

    private string _dialogBackground = "#E3E5EE";
    private string _borderColor = "#D5D9E0";
    private string _primaryTextBrush = "#111827";
    private string _secondaryTextBrush = "#5E6777";
    private string _buttonBackground = "#F8FAFC";
    private string _buttonBorderBrush = "#CBD5E1";
    private string _buttonForeground = "#111827";

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<PythonEnvRow> Environments { get; } = [];

    public string DialogBackground
    {
        get => _dialogBackground;
        private set => SetAndRaise(ref _dialogBackground, value, nameof(DialogBackground));
    }

    public string BorderColor
    {
        get => _borderColor;
        private set => SetAndRaise(ref _borderColor, value, nameof(BorderColor));
    }

    public string PrimaryTextBrush
    {
        get => _primaryTextBrush;
        private set => SetAndRaise(ref _primaryTextBrush, value, nameof(PrimaryTextBrush));
    }

    public string SecondaryTextBrush
    {
        get => _secondaryTextBrush;
        private set => SetAndRaise(ref _secondaryTextBrush, value, nameof(SecondaryTextBrush));
    }

    public string ButtonBackground
    {
        get => _buttonBackground;
        private set => SetAndRaise(ref _buttonBackground, value, nameof(ButtonBackground));
    }

    public string ButtonBorderBrush
    {
        get => _buttonBorderBrush;
        private set => SetAndRaise(ref _buttonBorderBrush, value, nameof(ButtonBorderBrush));
    }

    public string ButtonForeground
    {
        get => _buttonForeground;
        private set => SetAndRaise(ref _buttonForeground, value, nameof(ButtonForeground));
    }

    public PythonEnvManagerSettingsDialogWindow()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        RefreshViewModelBinding();

        if (_field is not null)
        {
            RebuildEnvironmentList(_field.Value ?? string.Empty);
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_field is not null)
        {
            _field.PropertyChanged -= OnFieldPropertyChanged;
        }

        if (DataContext is not EditorDialogField field)
        {
            _field = null;
            return;
        }

        _field = field;
        RefreshViewModelBinding();

        _field.PropertyChanged += OnFieldPropertyChanged;
        RebuildEnvironmentList(_field.Value ?? string.Empty);
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        AttachToViewModel(null);

        if (_field is not null)
        {
            _field.PropertyChanged -= OnFieldPropertyChanged;
        }
    }

    private void RefreshViewModelBinding()
    {
        var root = this.FindAncestorOfType<EditorPropertyDialog>();
        _viewModel = root?.DataContext as MainWindowViewModel;
        AttachToViewModel(_viewModel);
    }

    private void OnFieldPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditorDialogField.Value) && _field is not null)
        {
            RebuildEnvironmentList(_field.Value ?? string.Empty);
        }
    }

    private void AttachToViewModel(MainWindowViewModel? viewModel)
    {
        if (ReferenceEquals(_subscribedViewModel, viewModel))
        {
            ApplyViewModelTheme(viewModel);
            return;
        }

        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _subscribedViewModel = viewModel;

        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        ApplyViewModelTheme(viewModel);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.DialogBackground)
            or nameof(MainWindowViewModel.CardBorderBrush)
            or nameof(MainWindowViewModel.PrimaryTextBrush)
            or nameof(MainWindowViewModel.SecondaryTextBrush)
            or nameof(MainWindowViewModel.EditPanelButtonBackground)
            or nameof(MainWindowViewModel.EditPanelButtonBorderBrush)
            or nameof(MainWindowViewModel.IsDarkTheme))
        {
            ApplyViewModelTheme(_subscribedViewModel);
        }
    }

    private void ApplyViewModelTheme(MainWindowViewModel? viewModel)
    {
        if (viewModel is null)
        {
            return;
        }

        DialogBackground = viewModel.DialogBackground;
        BorderColor = viewModel.CardBorderBrush;
        PrimaryTextBrush = viewModel.PrimaryTextBrush;
        SecondaryTextBrush = viewModel.SecondaryTextBrush;
        ButtonBackground = viewModel.EditPanelButtonBackground;
        ButtonBorderBrush = viewModel.EditPanelButtonBorderBrush;
        ButtonForeground = viewModel.PrimaryTextBrush;

        foreach (var row in Environments)
        {
            row.OnThemeChanged();
        }
    }

    private void RebuildEnvironmentList(string rawDefinitions)
    {
        Environments.Clear();

        if (string.IsNullOrWhiteSpace(rawDefinitions))
        {
            return;
        }

        var ownerItem = _field?.OwnerItem ?? _viewModel?.SelectedItem;
        var baseDirectory = ResolveWorkspaceDirectory(ownerItem);

        foreach (var row in PythonEnvManagerRuntime.BuildRows(ownerItem, rawDefinitions, baseDirectory))
        {
            Environments.Add(row);
        }
    }

    private string ResolveWorkspaceDirectory(FolderItemModel? ownerItem)
    {
        if (!string.IsNullOrWhiteSpace(_field?.OwnerWorkspaceDirectory))
        {
            return _field.OwnerWorkspaceDirectory;
        }

        return _viewModel?.ResolveWorkspaceDirectory(ownerItem) ?? ResolveWorkspaceFallbackDirectory();
    }

    private static string ResolveWorkspaceFallbackDirectory()
    {
        var projectRoot = Core.OpenedDirectory;
        if (!string.IsNullOrWhiteSpace(projectRoot))
        {
            var folderRoot = Path.Combine(projectRoot, "Folder");
            return Directory.Exists(folderRoot) ? folderRoot : projectRoot;
        }

        return AppContext.BaseDirectory;
    }

    private async void OnAddEnvironmentClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not EditorDialogField field)
        {
            return;
        }

        // Resolve MainWindowViewModel from ancestor property dialog or from the top-level window
        var vm = _viewModel
                 ?? this.FindAncestorOfType<EditorPropertyDialog>()?.DataContext as MainWindowViewModel
                 ?? TopLevel.GetTopLevel(this)?.DataContext as MainWindowViewModel;

        if (vm is null)
        {
            return;
        }

        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        _viewModel = vm;
        _field ??= field;

        var existingNames = PythonEnvDefinitionHelper.ParseDefinitions(_field.Value ?? string.Empty)
            .Select(env => env.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray();

        var picker = new PythonEnvPickerDialogWindow(vm, existingNames);
        var definition = await picker.ShowDialog<string?>(owner);
        if (string.IsNullOrWhiteSpace(definition))
        {
            return;
        }

        // Update underlying field value; this will trigger RebuildEnvironmentList via property changed handler
        if (string.IsNullOrWhiteSpace(_field.Value))
        {
            _field.Value = definition;
        }
        else
        {
            _field.Value = _field.Value.TrimEnd() + Environment.NewLine + definition;
        }

        var ownerItem = _field.OwnerItem ?? _viewModel?.SelectedItem;
        if (ownerItem?.PythonEnvAutoStart == true)
        {
            var addedDefinition = PythonEnvDefinitionHelper.ParseDefinitions(definition).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(addedDefinition.ScriptPath))
            {
                var baseDirectory = ResolveWorkspaceDirectory(ownerItem);
                var effectivePath = PythonEnvDefinitionHelper.ResolveScriptPath(addedDefinition.ScriptPath, baseDirectory);
                PythonEnvRowRegistry.GetOrCreate(addedDefinition.Name, addedDefinition.ScriptPath, effectivePath, ownerItem).Start();
            }
        }

        e.Handled = true;
    }

    private async void OnDeleteEnvironmentClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: PythonEnvRow row })
        {
            return;
        }

        if (_field is null)
        {
            return;
        }

        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var confirmed = await EditorInputDialogs.ConfirmAsync(
            owner,
            $"Delete environment '{row.Name}'?",
            "The environment will be stopped, removed from the configuration, and its folder under ./Python will be deleted.",
            confirmText: "Delete",
            cancelText: "Cancel");

        if (!confirmed)
        {
            return;
        }

        var ownerItem = _field.OwnerItem ?? _viewModel?.SelectedItem;
        var baseDirectory = ResolveWorkspaceDirectory(ownerItem);

        try
        {
            row.Stop();
        }
        catch (Exception ex)
        {
            Core.LogWarn($"[PythonEnvManager] Failed to stop environment '{row.Name}' before delete.", ex);
        }

        TryDeleteEnvironmentDirectory(row, baseDirectory);

        _field.Value = PythonEnvDefinitionHelper.RemoveDefinition(_field.Value ?? string.Empty, row.Name, row.ScriptPath);
        PythonEnvRowRegistry.Remove(ownerItem, row.ScriptPath, row.EffectiveScriptPath);

        e.Handled = true;
    }

    private static void TryDeleteEnvironmentDirectory(PythonEnvRow row, string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            return;
        }

        try
        {
            var fullScriptPath = PythonEnvDefinitionHelper.ResolveScriptPath(row.ScriptPath, baseDirectory);
            if (string.IsNullOrWhiteSpace(fullScriptPath))
            {
                return;
            }

            var envDirectory = Path.GetDirectoryName(fullScriptPath);
            if (string.IsNullOrWhiteSpace(envDirectory) || !Directory.Exists(envDirectory))
            {
                return;
            }

            var fullEnvDirectory = Path.GetFullPath(envDirectory);
            var pythonRoot = Path.GetFullPath(Path.Combine(baseDirectory, "Python"));
            var pythonRootWithSeparator = pythonRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            if (!fullEnvDirectory.StartsWith(pythonRootWithSeparator, StringComparison.OrdinalIgnoreCase))
            {
                Core.LogWarn($"[PythonEnvManager] Skipping delete outside Python root: {fullEnvDirectory}");
                return;
            }

            Directory.Delete(fullEnvDirectory, recursive: true);
        }
        catch (Exception ex)
        {
            Core.LogWarn($"[PythonEnvManager] Failed to delete environment directory for '{row.Name}'.", ex);
        }
    }

    private void SetAndRaise<T>(ref T field, T value, string propertyName)
    {
        if (Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

internal static class PythonEnvDefinitionHelper
{
    public static IReadOnlyList<(string Name, string ScriptPath)> ParseDefinitions(string definitions)
    {
        var result = new List<(string Name, string ScriptPath)>();
        var lines = definitions.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var parts = line.Split('|');
            var scriptPart = parts.Length > 1 ? parts[1] : parts[0];
            var namePart = parts.Length > 1 ? parts[0] : string.Empty;

            var scriptPath = scriptPart.Trim();
            if (string.IsNullOrWhiteSpace(scriptPath))
            {
                continue;
            }

            var name = string.IsNullOrWhiteSpace(namePart)
                ? Path.GetFileNameWithoutExtension(scriptPath)
                : namePart.Trim();

            result.Add((name, scriptPath));
        }

        return result;
    }

    public static string ResolveScriptPath(string scriptPath, string? baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(scriptPath) || Path.IsPathRooted(scriptPath) || string.IsNullOrWhiteSpace(baseDirectory))
        {
            return scriptPath;
        }

        return Path.Combine(baseDirectory, scriptPath);
    }

    public static string SerializeDefinitions(IEnumerable<(string Name, string ScriptPath)> definitions)
        => string.Join(Environment.NewLine, definitions.Select(static env => $"{env.Name} | {env.ScriptPath}"));

    public static string RemoveDefinition(string definitions, string name, string scriptPath)
    {
        var lines = definitions.Replace("\r\n", "\n").Split('\n');
        var remainingLines = new List<string>(lines.Length);

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            if (!TryParseDefinitionLine(line, out var definition) || !IsSameDefinition(definition, name, scriptPath))
            {
                remainingLines.Add(line);
            }
        }

        return string.Join(Environment.NewLine, remainingLines).Trim();
    }

    private static bool IsSameDefinition((string Name, string ScriptPath) definition, string name, string scriptPath)
        => string.Equals(definition.Name?.Trim(), name?.Trim(), StringComparison.OrdinalIgnoreCase)
           && string.Equals(definition.ScriptPath?.Trim(), scriptPath?.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool TryParseDefinitionLine(string rawLine, out (string Name, string ScriptPath) definition)
    {
        definition = default;

        var line = rawLine.Trim();
        if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
        {
            return false;
        }

        var parts = line.Split('|');
        var scriptPart = parts.Length > 1 ? parts[1] : parts[0];
        var namePart = parts.Length > 1 ? parts[0] : string.Empty;

        var scriptPath = scriptPart.Trim();
        if (string.IsNullOrWhiteSpace(scriptPath))
        {
            return false;
        }

        var name = string.IsNullOrWhiteSpace(namePart)
            ? Path.GetFileNameWithoutExtension(scriptPath)
            : namePart.Trim();

        definition = (name, scriptPath);
        return true;
    }

    public static string? TryResolveLayoutDirectory(string? layoutPath)
    {
        if (string.IsNullOrWhiteSpace(layoutPath))
        {
            return null;
        }

        try
        {
            var fullLayoutPath = Path.GetFullPath(layoutPath);
            return Path.GetDirectoryName(fullLayoutPath);
        }
        catch
        {
            return null;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using HornetStudio.Editor.Helpers;
using HornetStudio.Editor.ViewModels;

namespace HornetStudio.Editor.Widgets;

public sealed class IconPickerResult
{
    public string? IconPath { get; init; }

    public string? IconColor { get; init; }
}

public sealed partial class IconPickerDialogWindow : Window, INotifyPropertyChanged
{
    private static readonly IReadOnlyList<Color> StandardColors =
    [
        Color.Parse("#1B19D8"),
        Color.Parse("#C62828"),
        Color.Parse("#118C22"),
        Color.Parse("#7E3CCB"),
        Color.Parse("#FFFFFF"),
        Color.Parse("#3A8EE6"),
        Color.Parse("#5F7433"),
        Color.Parse("#4F477D"),
        Color.Parse("#A55B12"),
        Color.Parse("#FFAF1A"),
        Color.Parse("#111111"),
        Color.Parse("#FF1F92"),
        Color.Parse("#6B63C8"),
        Color.Parse("#D46BD4"),
        Color.Parse("#B8860B"),
        Color.Parse("#8FAFB1"),
        Color.Parse("#FF1A1A"),
        Color.Parse("#FFFF00"),
        Color.Parse("#BDBDBD"),
        Color.Parse("#EAEAEA")
    ];

    private MainWindowViewModel? _viewModel;
    private string _dialogBackground = "#E3E5EE";
    private string _borderColor = "#D5D9E0";
    private string _primaryTextBrush = "#111827";
    private string _secondaryTextBrush = "#5E6777";
    private string _buttonBackground = "#F8FAFC";
    private string _buttonBorderBrush = "#CBD5E1";
    private string _buttonForeground = "#111827";
    private string _inputBackground = "#FFFFFF";
    private string _inputForeground = "#111827";
    private string _parameterHoverColor = "#BDBDBD";
    private string _editorDialogSectionContentBackground = "#EEF3F8";
    private string _sectionBorderBrush = "#CBD5E1";
    private string _sectionHeaderForeground = "#111827";
    private string? _iconColor;
    private IconPickerItem? _selectedIconItem;
    private string _filterText = string.Empty;
    private string _infoMessage = string.Empty;
    private readonly List<IconPickerItem> _allIconItems = new();

    public IconPickerDialogWindow()
        : this(null, null)
    {
    }

    public new event PropertyChangedEventHandler? PropertyChanged;

    public IconPickerDialogWindow(MainWindowViewModel? viewModel, string? currentIconPath)
    {
        IconItems = [];
        InitializeComponent();
        DataContext = this;
        AttachToViewModel(viewModel);
        LoadIcons();
        BuildColorPalette();

        if (!string.IsNullOrWhiteSpace(currentIconPath))
        {
            var normalizedStoredPath = IconPathHelper.NormalizeStoredPath(currentIconPath, GetCurrentLayoutPath());
            SelectedIconItem = IconItems.FirstOrDefault(item => string.Equals(item.StoredPath, normalizedStoredPath, StringComparison.OrdinalIgnoreCase));
        }
    }

    public ObservableCollection<IconPickerItem> IconItems { get; }

    public IconPickerItem? SelectedIconItem
    {
        get => _selectedIconItem;
        set
        {
            if (Equals(_selectedIconItem, value))
            {
                return;
            }

            _selectedIconItem = value;
            OnPropertyChanged(nameof(SelectedIconItem));
            OnPropertyChanged(nameof(SelectedIconPath));
            OnPropertyChanged(nameof(SelectedPreviewIconPath));
            OnPropertyChanged(nameof(CanSelect));
        }
    }

    public string? SelectedIconPath => SelectedIconItem?.StoredPath;

    public string? SelectedPreviewIconPath => SelectedIconItem?.ActualPath;

    public string? IconColor
    {
        get => _iconColor;
        set
        {
            if (_iconColor == value)
            {
                return;
            }

            _iconColor = value;
            OnPropertyChanged(nameof(IconColor));
            OnPropertyChanged(nameof(IconColorPreviewBrush));
            OnPropertyChanged(nameof(EffectiveIconColor));
        }
    }

    public bool CanSelect => SelectedIconItem is not null;

    public string FilterText
    {
        get => _filterText;
        set
        {
            var normalized = value ?? string.Empty;
            if (string.Equals(_filterText, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _filterText = normalized;
            OnPropertyChanged(nameof(FilterText));
            ApplyFilter();
        }
    }

    public string InfoMessage
    {
        get => _infoMessage;
        private set => SetAndRaise(ref _infoMessage, value, nameof(InfoMessage));
    }

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

    public string InputBackground
    {
        get => _inputBackground;
        private set => SetAndRaise(ref _inputBackground, value, nameof(InputBackground));
    }

    public string InputForeground
    {
        get => _inputForeground;
        private set => SetAndRaise(ref _inputForeground, value, nameof(InputForeground));
    }

    public string ParameterHoverColor
    {
        get => _parameterHoverColor;
        private set => SetAndRaise(ref _parameterHoverColor, value, nameof(ParameterHoverColor));
    }

    public string EditorDialogSectionContentBackground
    {
        get => _editorDialogSectionContentBackground;
        private set => SetAndRaise(ref _editorDialogSectionContentBackground, value, nameof(EditorDialogSectionContentBackground));
    }

    public string SectionBorderBrush
    {
        get => _sectionBorderBrush;
        private set => SetAndRaise(ref _sectionBorderBrush, value, nameof(SectionBorderBrush));
    }

    public string SectionHeaderForeground
    {
        get => _sectionHeaderForeground;
        private set => SetAndRaise(ref _sectionHeaderForeground, value, nameof(SectionHeaderForeground));
    }

    public string? EffectiveIconColor
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(IconColor))
            {
                return IconColor;
            }

            if (_viewModel is null)
            {
                return null;
            }

            // Theme rule: dark -> white, light -> black
            return _viewModel.IsDarkTheme ? "#FFFFFF" : "#000000";
        }
    }

    public IBrush IconColorPreviewBrush
    {
        get
        {
            if (string.IsNullOrWhiteSpace(IconColor))
            {
                return Brushes.Transparent;
            }

            return Color.TryParse(IconColor, out var color)
                ? new SolidColorBrush(color)
                : Brushes.Transparent;
        }
    }

    protected override void OnClosed(System.EventArgs e)
    {
        AttachToViewModel(null);
        base.OnClosed(e);
    }

    private void AttachToViewModel(MainWindowViewModel? viewModel)
    {
        if (ReferenceEquals(_viewModel, viewModel))
        {
            return;
        }

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = viewModel;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        UpdateThemeBindings();
        UpdateWindowIcon();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.DialogBackground)
            || e.PropertyName == nameof(MainWindowViewModel.CardBorderBrush)
            || e.PropertyName == nameof(MainWindowViewModel.PrimaryTextBrush)
            || e.PropertyName == nameof(MainWindowViewModel.SecondaryTextBrush)
            || e.PropertyName == nameof(MainWindowViewModel.EditPanelButtonBackground)
            || e.PropertyName == nameof(MainWindowViewModel.EditPanelButtonBorderBrush)
            || e.PropertyName == nameof(MainWindowViewModel.ParameterEditBackgrundColor)
            || e.PropertyName == nameof(MainWindowViewModel.ParameterEditForeColor)
            || e.PropertyName == nameof(MainWindowViewModel.ParameterHoverColor)
            || e.PropertyName == nameof(MainWindowViewModel.EditorDialogSectionContentBackground)
            || e.PropertyName == nameof(MainWindowViewModel.EditorDialogSectionHeaderBorderBrush)
            || e.PropertyName == nameof(MainWindowViewModel.EditorDialogSectionHeaderForeground)
            || e.PropertyName == nameof(MainWindowViewModel.IsDarkTheme))
        {
            UpdateThemeBindings();
            if (e.PropertyName == nameof(MainWindowViewModel.IsDarkTheme))
            {
                UpdateWindowIcon();
            }
        }

        if (e.PropertyName == nameof(MainWindowViewModel.SelectedFolder))
        {
            LoadIcons();
        }
    }

    private void UpdateWindowIcon()
    {
        if (_viewModel is null)
        {
            return;
        }

        var iconName = _viewModel.IsDarkTheme ? "listDark.png" : "listLight.png";
        var uri = new Uri($"avares://HornetStudio.Editor/EditorIcons/{iconName}");

        try
        {
            using var stream = AssetLoader.Open(uri);
            Icon = new WindowIcon(new Bitmap(stream));
        }
        catch
        {
            // If the PNGs are missing or cannot be loaded, keep the default icon.
        }
    }

    private void UpdateThemeBindings()
    {
        DialogBackground = _viewModel?.DialogBackground ?? "#E3E5EE";
        BorderColor = _viewModel?.CardBorderBrush ?? "#D5D9E0";
        PrimaryTextBrush = _viewModel?.PrimaryTextBrush ?? "#111827";
        SecondaryTextBrush = _viewModel?.SecondaryTextBrush ?? "#5E6777";
        ButtonBackground = _viewModel?.EditPanelButtonBackground ?? "#F8FAFC";
        ButtonBorderBrush = _viewModel?.EditPanelButtonBorderBrush ?? "#CBD5E1";
        ButtonForeground = _viewModel?.PrimaryTextBrush ?? "#111827";
        InputBackground = _viewModel?.ParameterEditBackgrundColor ?? "#FFFFFF";
        InputForeground = _viewModel?.ParameterEditForeColor ?? "#111827";
        ParameterHoverColor = _viewModel?.ParameterHoverColor ?? "#BDBDBD";
        EditorDialogSectionContentBackground = _viewModel?.EditorDialogSectionContentBackground ?? "#EEF3F8";
        SectionBorderBrush = _viewModel?.EditorDialogSectionHeaderBorderBrush ?? "#CBD5E1";
        SectionHeaderForeground = _viewModel?.EditorDialogSectionHeaderForeground ?? "#111827";
        OnPropertyChanged(nameof(EffectiveIconColor));
    }

    private void LoadIcons()
    {
        try
        {
            var selectedIconPath = SelectedIconItem?.StoredPath;

            _allIconItems.Clear();
            IconItems.Clear();

            AddApplicationIconItems();

            foreach (var iconDirectory in GetIconDirectories())
            {
                if (!Directory.Exists(iconDirectory))
                {
                    continue;
                }

                foreach (var file in Directory.EnumerateFiles(iconDirectory, "*.svg"))
                {
                    AddIconItem(file);
                }
            }

            ApplyFilter();
            if (!string.IsNullOrWhiteSpace(selectedIconPath))
            {
                SelectedIconItem = IconItems.FirstOrDefault(item => string.Equals(item.StoredPath, selectedIconPath, StringComparison.OrdinalIgnoreCase));
            }
        }
        catch
        {
            // ignore IO errors for now
        }
    }

    private void OnSelectClicked(object? sender, RoutedEventArgs e)
    {
        var result = new IconPickerResult
        {
            IconPath = SelectedIconPath,
            IconColor = IconColor
        };

        Close(result);
        e.Handled = true;
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        Close(null);
        e.Handled = true;
    }

    private void SetAndRaise<T>(ref T field, T value, string propertyName)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void ApplyFilter()
    {
        IconItems.Clear();

        var query = _filterText.Trim();
        IEnumerable<IconPickerItem> items = _allIconItems;
        if (!string.IsNullOrWhiteSpace(query))
        {
            items = items.Where(item => item.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var item in items.OrderBy(i => i.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            IconItems.Add(item);
        }
    }

    private void OnIconListDragOver(object? sender, DragEventArgs e)
    {
        var hasSvg = GetDroppedSvgPaths(e.Data).Count > 0;
        if (!hasSvg)
        {
            return;
        }

        e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void OnIconListDrop(object? sender, DragEventArgs e)
    {
        var files = GetDroppedSvgPaths(e.Data);
        if (files.Count == 0)
        {
            return;
        }

        var iconDir = GetActiveFolderIconDirectory();
        if (string.IsNullOrWhiteSpace(iconDir))
        {
            ShowInfoMessage("Active folder icon directory is not available.");
            e.Handled = true;
            return;
        }

        Directory.CreateDirectory(iconDir);

        var added = 0;
        string? firstAddedPath = null;

        foreach (var file in files)
        {
            if (!string.Equals(Path.GetExtension(file), ".svg", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                var targetPath = Path.Combine(iconDir, Path.GetFileName(file));
                if (!File.Exists(targetPath))
                {
                    File.Copy(file, targetPath);
                }

                var storedTargetPath = IconPathHelper.NormalizeStoredPath(targetPath, GetCurrentLayoutPath());
                if (_allIconItems.All(item => !string.Equals(item.StoredPath, storedTargetPath, StringComparison.OrdinalIgnoreCase)))
                {
                    added++;
                    firstAddedPath ??= storedTargetPath;
                }
            }
            catch
            {
                // ignore individual copy errors
            }
        }

        if (added > 0)
        {
            LoadIcons();
            if (!string.IsNullOrWhiteSpace(firstAddedPath))
            {
                SelectedIconItem = IconItems.FirstOrDefault(item => string.Equals(item.StoredPath, firstAddedPath, StringComparison.OrdinalIgnoreCase));
            }

            ShowInfoMessage($"Added {added} icon(s).");
        }

        e.Handled = true;
    }

    private IEnumerable<string> GetIconDirectories()
    {
        var activeFolderIconDirectory = GetActiveFolderIconDirectory();
        if (!string.IsNullOrWhiteSpace(activeFolderIconDirectory))
        {
            yield return activeFolderIconDirectory;
        }
    }

    private string? GetActiveFolderIconDirectory()
        => IconPathHelper.GetFolderIconDirectory(GetCurrentLayoutPath());

    private string? GetCurrentLayoutPath()
        => _viewModel?.SelectedFolder?.UiFilePath;

    private void AddIconItem(string iconPath)
    {
        var normalizedPath = Path.GetFullPath(iconPath);
        var storedPath = IconPathHelper.NormalizeStoredPath(normalizedPath, GetCurrentLayoutPath());
        AddIconItem(normalizedPath, storedPath);
    }

    private void AddApplicationIconItems()
    {
        var iconRoot = new Uri("avares://HornetStudio.Editor/EditorIcons");
        foreach (var asset in AssetLoader.GetAssets(iconRoot, null))
        {
            var assetPath = asset.ToString();
            if (!assetPath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            AddIconItem(assetPath, assetPath);
        }
    }

    private void AddIconItem(string actualPath, string storedPath)
    {
        if (_allIconItems.Any(item => string.Equals(item.StoredPath, storedPath, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _allIconItems.Add(new IconPickerItem(actualPath, storedPath));
    }

    private static IReadOnlyList<string> GetDroppedSvgPaths(IDataObject data)
    {
        var paths = new List<string>();

        foreach (var file in data.GetFiles() ?? [])
        {
            var localPath = file.TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(localPath)
                && string.Equals(Path.GetExtension(localPath), ".svg", StringComparison.OrdinalIgnoreCase))
            {
                paths.Add(localPath);
            }
        }

        return paths;
    }

    private void OnOpenColorPickerClicked(object? sender, RoutedEventArgs e)
    {
        BuildColorPalette();

        if (this.FindControl<TextBox>("ColorHexTextBox") is { } hexBox)
        {
            hexBox.Text = IconColor ?? string.Empty;
        }

        if (this.FindControl<Popup>("ColorPopup") is { } popup && sender is Control control)
        {
            popup.PlacementTarget = control;
            popup.IsOpen = true;
        }

        e.Handled = true;
    }

    private void OnTransparentColorClicked(object? sender, RoutedEventArgs e)
    {
        IconColor = "Transparent";
        CloseColorPopup();
        e.Handled = true;
    }

    private void OnDefaultColorClicked(object? sender, RoutedEventArgs e)
    {
        IconColor = string.Empty;
        CloseColorPopup();
        e.Handled = true;
    }

    private void OnApplyHexColorClicked(object? sender, RoutedEventArgs e)
    {
        ApplyHexColorFromEditor();
        e.Handled = true;
    }

    private void OnColorHexTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        ApplyHexColorFromEditor();
        e.Handled = true;
    }

    private void ApplyHexColorFromEditor()
    {
        if (this.FindControl<TextBox>("ColorHexTextBox") is not { } hexBox)
        {
            return;
        }

        IconColor = NormalizeColorText(hexBox.Text);
        CloseColorPopup();
    }

    private void BuildColorPalette()
    {
        if (this.FindControl<WrapPanel>("ColorPalettePanel") is not { } panel || panel.Children.Count > 0)
        {
            return;
        }

        foreach (var color in StandardColors)
        {
            var colorText = ToHex(color);
            var button = new Button
            {
                Width = 32,
                Height = 32,
                Padding = new Thickness(0),
                Margin = new Thickness(0, 0, 6, 6),
                Tag = colorText,
                Content = new Border
                {
                    Width = 24,
                    Height = 24,
                    Background = new SolidColorBrush(color),
                        BorderBrush = Brushes.White,
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(6)
                },
                    Background = Brushes.Transparent,
                    BorderBrush = Brushes.Transparent,
                    BorderThickness = new Thickness(0)
            };

                button.Click += OnPaletteColorClicked;
            panel.Children.Add(button);
        }
    }

        private void OnPaletteColorClicked(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string colorText })
            {
                return;
            }

            IconColor = colorText;
            CloseColorPopup();
            e.Handled = true;
        }

    private void CloseColorPopup()
    {
        if (this.FindControl<Popup>("ColorPopup") is { } popup)
        {
            popup.IsOpen = false;
        }
    }

    private static string NormalizeColorText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        if (string.Equals(trimmed, "Transparent", StringComparison.OrdinalIgnoreCase))
        {
            return "Transparent";
        }

        return Color.TryParse(trimmed, out var color)
            ? ToHex(color)
            : trimmed;
    }

    private static string ToHex(Color color)
        => color.A == byte.MaxValue
            ? $"#{color.R:X2}{color.G:X2}{color.B:X2}"
            : $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";

    private void ShowInfoMessage(string message)
    {
        InfoMessage = message;

        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };

        timer.Tick += (_, _) =>
        {
            InfoMessage = string.Empty;
            timer.Stop();
        };

        timer.Start();
    }
}

public sealed class IconPickerItem
{
    public IconPickerItem(string actualPath, string storedPath)
    {
        ActualPath = actualPath;
        StoredPath = storedPath;
        DisplayName = Path.GetFileNameWithoutExtension(actualPath);
    }

    public string ActualPath { get; }

    public string StoredPath { get; }

    public string DisplayName { get; }
}

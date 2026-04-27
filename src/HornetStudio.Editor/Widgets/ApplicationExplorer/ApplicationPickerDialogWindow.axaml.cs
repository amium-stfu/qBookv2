using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using HornetStudio.Editor.ViewModels;

namespace HornetStudio.Editor.Widgets;

public sealed class ApplicationTemplateItem
{
    public required string DisplayName { get; init; }

    public string? TemplatePath { get; init; }
}

public sealed partial class ApplicationPickerDialogWindow : Window, INotifyPropertyChanged
{
    private readonly MainWindowViewModel? _viewModel;
    private readonly HashSet<string> _existingNames;
    private MainWindowViewModel? _subscribedViewModel;
    private string _envName = string.Empty;
    private ApplicationTemplateItem? _selectedTemplate;
    private string _summary = string.Empty;
    private string _errorMessage = string.Empty;
    private string _templatePreview = string.Empty;
    private string _dialogBackground = "#FFFFFF";
    private string _panelBackground = "#F8FAFC";
    private string _borderColor = "#CBD5E1";
    private string _primaryTextBrush = "#111827";
    private string _secondaryTextBrush = "#5E6777";
    private string _editorBackground = "#FFFFFF";
    private string _editorForeground = "#111827";
    private string _buttonBackground = "#F8FAFC";
    private string _buttonBorderBrush = "#CBD5E1";
    private string _buttonForeground = "#111827";

    public ApplicationPickerDialogWindow()
        : this(null, null)
    {
    }

    public ApplicationPickerDialogWindow(MainWindowViewModel? viewModel, IEnumerable<string>? existingNames = null)
    {
        _viewModel = viewModel;
        _existingNames = new HashSet<string>(existingNames ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        Templates = [];
        InitializeComponent();
        DataContext = this;
        AttachToViewModel(_viewModel);
        LoadTemplates();
        UpdateSummary();
        UpdateTemplatePreview();
        Closed += OnClosed;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ApplicationTemplateItem> Templates { get; }

    public string EnvName
    {
        get => _envName;
        set
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (string.Equals(_envName, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _envName = normalized;
            OnPropertyChanged(nameof(EnvName));
            OnPropertyChanged(nameof(CanSave));
            UpdateSummary();
            UpdateTemplatePreview();
        }
    }

    public ApplicationTemplateItem? SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            if (Equals(_selectedTemplate, value))
            {
                return;
            }

            _selectedTemplate = value;
            OnPropertyChanged(nameof(SelectedTemplate));
            OnPropertyChanged(nameof(CanSave));
            UpdateSummary();
            UpdateTemplatePreview();
        }
    }

    public string Summary
    {
        get => _summary;
        private set => SetAndRaise(ref _summary, value, nameof(Summary));
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetAndRaise(ref _errorMessage, value, nameof(ErrorMessage));
    }

    public string TemplatePreview
    {
        get => _templatePreview;
        private set => SetAndRaise(ref _templatePreview, value, nameof(TemplatePreview));
    }

    public string DialogBackground
    {
        get => _dialogBackground;
        private set => SetAndRaise(ref _dialogBackground, value, nameof(DialogBackground));
    }

    public string PanelBackground
    {
        get => _panelBackground;
        private set => SetAndRaise(ref _panelBackground, value, nameof(PanelBackground));
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

    public string EditorBackground
    {
        get => _editorBackground;
        private set => SetAndRaise(ref _editorBackground, value, nameof(EditorBackground));
    }

    public string EditorForeground
    {
        get => _editorForeground;
        private set => SetAndRaise(ref _editorForeground, value, nameof(EditorForeground));
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

    public bool CanSave => !string.IsNullOrWhiteSpace(EnvName) && SelectedTemplate is not null;

    private void LoadTemplates()
    {
        Templates.Clear();

        if (_viewModel is null)
        {
            Templates.Add(new ApplicationTemplateItem
            {
                DisplayName = "New",
                TemplatePath = null
            });

            SelectedTemplate = Templates.FirstOrDefault();
            return;
        }

        try
        {
            var templatesDir = _viewModel.GetPythonTemplatesDirectory();
            if (Directory.Exists(templatesDir))
            {
                foreach (var file in Directory.EnumerateFiles(templatesDir, "*.py"))
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    var displayName = string.Equals(name, "PythonApplicationNew", StringComparison.OrdinalIgnoreCase)
                                      || string.Equals(name, "PythonEnvNew", StringComparison.OrdinalIgnoreCase)
                        ? "New"
                        : name;

                    Templates.Add(new ApplicationTemplateItem
                    {
                        DisplayName = displayName,
                        TemplatePath = file
                    });
                }
            }
        }
        catch
        {
            // Ignore IO errors here; the dialog will still offer a basic option below.
        }

        if (Templates.Count == 0)
        {
            Templates.Add(new ApplicationTemplateItem
            {
                DisplayName = "New",
                TemplatePath = null
            });
        }

        SelectedTemplate = Templates.FirstOrDefault();
    }

    private void UpdateSummary()
    {
        if (string.IsNullOrWhiteSpace(EnvName))
        {
            Summary = "Enter a name for the application.";
            return;
        }

        var templateName = SelectedTemplate?.DisplayName ?? "New";
        Summary = $"Application '{EnvName}' will be created using template '{templateName}'.";
    }

    private void UpdateTemplatePreview()
    {
        var templatePath = SelectedTemplate?.TemplatePath;
        if (!string.IsNullOrWhiteSpace(templatePath) && File.Exists(templatePath))
        {
            try
            {
                TemplatePreview = File.ReadAllText(templatePath);
                return;
            }
            catch
            {
                // Fall through to the generic preview.
            }
        }

        var displayName = string.IsNullOrWhiteSpace(EnvName) ? "Env" : EnvName;
        TemplatePreview = "# Python application script" + Environment.NewLine + Environment.NewLine
            + "from ui_python_client import PythonClient" + Environment.NewLine + Environment.NewLine
            + $"client = PythonClient(name=\"{displayName.Replace("\"", "'")}\")" + Environment.NewLine + Environment.NewLine
            + "if __name__ == \"__main__\":" + Environment.NewLine
            + "    raise SystemExit(client.run())" + Environment.NewLine;
    }

    private void AttachToViewModel(MainWindowViewModel? viewModel)
    {
        if (ReferenceEquals(_subscribedViewModel, viewModel))
        {
            ApplyTheme(viewModel);
            UpdateWindowIcon(viewModel);
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

        ApplyTheme(viewModel);
        UpdateWindowIcon(viewModel);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not MainWindowViewModel vm)
        {
            return;
        }

        if (e.PropertyName is nameof(MainWindowViewModel.IsDarkTheme)
            or nameof(MainWindowViewModel.DialogBackground)
            or nameof(MainWindowViewModel.CardBorderBrush)
            or nameof(MainWindowViewModel.PrimaryTextBrush)
            or nameof(MainWindowViewModel.SecondaryTextBrush)
            or nameof(MainWindowViewModel.ParameterEditBackgrundColor)
            or nameof(MainWindowViewModel.ParameterEditForeColor)
            or nameof(MainWindowViewModel.EditPanelButtonBackground)
            or nameof(MainWindowViewModel.EditPanelButtonBorderBrush)
            or nameof(MainWindowViewModel.EditorDialogSectionContentBackground))
        {
            ApplyTheme(vm);
            UpdateWindowIcon(vm);
        }
    }

    private void ApplyTheme(MainWindowViewModel? viewModel)
    {
        if (viewModel is null)
        {
            return;
        }

        DialogBackground = viewModel.DialogBackground;
        PanelBackground = viewModel.EditorDialogSectionContentBackground;
        BorderColor = viewModel.CardBorderBrush;
        PrimaryTextBrush = viewModel.PrimaryTextBrush;
        SecondaryTextBrush = viewModel.SecondaryTextBrush;
        EditorBackground = viewModel.ParameterEditBackgrundColor;
        EditorForeground = viewModel.ParameterEditForeColor;
        ButtonBackground = viewModel.EditPanelButtonBackground;
        ButtonBorderBrush = viewModel.EditPanelButtonBorderBrush;
        ButtonForeground = viewModel.PrimaryTextBrush;
    }

    private void UpdateWindowIcon(MainWindowViewModel? viewModel)
    {
        if (viewModel is null)
        {
            return;
        }

        var iconName = viewModel.IsDarkTheme ? "cogDark.png" : "cogLight.png";
        var uri = new Uri($"avares://HornetStudio.Editor/EditorIcons/{iconName}");

        try
        {
            using var stream = AssetLoader.Open(uri);
            Icon = new WindowIcon(new Bitmap(stream));
        }
        catch
        {
            // Keep the default icon if the asset is unavailable.
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        AttachToViewModel(null);
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        Close((string?)null);
    }

    private void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        ErrorMessage = string.Empty;

        if (_viewModel is null)
        {
            Close((string?)null);
            return;
        }

        if (string.IsNullOrWhiteSpace(EnvName) || SelectedTemplate is null)
        {
            ErrorMessage = "Name and template are required.";
            return;
        }

        if (_existingNames.Contains(EnvName))
        {
            ErrorMessage = $"An application named '{EnvName}' already exists.";
            return;
        }

        try
        {
            var envDefinition = _viewModel.CreatePythonApplicationFromTemplate(EnvName, SelectedTemplate.TemplatePath);
            if (string.IsNullOrWhiteSpace(envDefinition))
            {
                ErrorMessage = "Failed to create application.";
                return;
            }

            Close(envDefinition);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private void OnPropertyChanged(string propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void SetAndRaise<T>(ref T field, T value, string propertyName)
    {
        if (Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }
}

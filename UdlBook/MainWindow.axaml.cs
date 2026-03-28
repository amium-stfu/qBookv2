using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Amium.Host;
using Amium.EditorUi.Controls;
using Amium.UiEditor.Widgets;
using UdlBook.ViewModels;

namespace UdlBook;

public partial class MainWindow : Window
{
    private LogWindow? _logWindow;
    private Window? _keyboardWindow;
    private TextBox? _keyboardTarget;
    private Action? _keyboardApplyAction;
    private Action? _keyboardCancelAction;

    public MainWindow()
    {
        InitializeComponent();
        Core.UiStateChanged += HandleHostUiStateChanged;
    }

    private async void OnDemoTextInputClicked(object? sender, RoutedEventArgs e)
    {
        await EditorInputDialogs.EditTextAsync(
            this,
            "Namen eintragen",
            "Username",
            string.Empty);
    }

    private async void OnDemoHexInputClicked(object? sender, RoutedEventArgs e)
    {
        await EditorInputDialogs.EditHexAsync(
            this,
            "Hex-Wert eingeben",
            "Demo",
            digits: 8,
            initialValue: null);
    }

    private async void OnDemoNumericIntClicked(object? sender, RoutedEventArgs e)
    {
        await EditorInputDialogs.EditNumericAsync(
            this,
            "Nummer eingeben",
            "Durchlauf",
            format: "0",
            initialValue: null);
    }

    private async void OnDemoNumericDoubleClicked(object? sender, RoutedEventArgs e)
    {
        await EditorInputDialogs.EditNumericAsync(
            this,
            "Wert eingeben",
            "Messwert",
            format: "0.00",
            initialValue: null);
    }

    private void OnMainMenuButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control)
        {
            return;
        }

        if (control.ContextMenu is { } menu)
        {
            menu.PlacementTarget = control;
            menu.Open();
        }
    }

    private async void UserLogin_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var password = await ShowUserPasswordDialogAsync();
        if (password is null)
        {
            return;
        }

        viewModel.TrySetUserByPassword(password);
    }

    private Window EnsureKeyboardWindow()
    {
        if (_keyboardWindow is { } existing)
        {
            return existing;
        }

        var keyboardHeight = Height * 0.28;
        var window = new Window
        {
            Width = Width,
            Height = keyboardHeight,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            ShowInTaskbar = false,
            SystemDecorations = SystemDecorations.None,
            Topmost = true
        };

        var pad = new EditorTextInputPad
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        pad.KeyInvoked += OnKeyboardKeyInvoked;
        pad.ActionInvoked += OnKeyboardActionInvoked;

        window.Content = pad;
        window.Closed += (_, _) => _keyboardWindow = null;

        _keyboardWindow = window;
        return window;
    }

    private void ShowKeyboard(TextBox target, Action applyAction, Action cancelAction)
    {
        _keyboardTarget = target;
        _keyboardApplyAction = applyAction;
        _keyboardCancelAction = cancelAction;

        var window = EnsureKeyboardWindow();

        var mainWidth = Width;
        var mainHeight = Height;
        var keyboardHeight = window.Height;

        window.Width = mainWidth;

        var topOffset = mainHeight - keyboardHeight;
        if (topOffset < 0)
        {
            topOffset = 0;
        }

        window.Position = new PixelPoint(Position.X, Position.Y + (int)topOffset);

        if (!window.IsVisible)
        {
            window.Show();
        }
        else
        {
            window.Activate();
        }
    }

    private void HideKeyboard()
    {
        _keyboardTarget = null;
        _keyboardApplyAction = null;
        _keyboardCancelAction = null;
        _keyboardWindow?.Hide();
    }

    private void OnKeyboardKeyInvoked(object? sender, string key)
    {
        if (_keyboardTarget is null)
        {
            return;
        }

        InsertTextAtCaret(_keyboardTarget, key);
        _keyboardTarget.Focus();
    }

    private void OnKeyboardActionInvoked(object? sender, string action)
    {
        if (_keyboardTarget is null)
        {
            return;
        }

        switch (action)
        {
            case "Backspace":
                BackspaceAtCaret(_keyboardTarget);
                _keyboardTarget.Focus();
                break;
            case "MoveLeft":
                MoveCaret(_keyboardTarget, -1);
                _keyboardTarget.Focus();
                break;
            case "Apply":
                _keyboardApplyAction?.Invoke();
                _keyboardTarget.Focus();
                break;
            case "Cancel":
                _keyboardCancelAction?.Invoke();
                break;
        }
    }

    private async void OnDemoPasswordTextClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var result = await EditorInputDialogs.EditTextAsync(this, header: "Enter password", subHeader: "Demo", initialValue: string.Empty, isPassword: true);
    }

    private async void OnDemoNumericPasswordClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var result = await EditorInputDialogs.EditNumericAsync(this, header: "Enter PIN", subHeader: "Demo", format: "0", initialValue: null, maskInput: true);
    }

    private static void InsertTextAtCaret(TextBox input, string value)
    {
        var text = input.Text ?? string.Empty;
        var start = input.SelectionStart;
        var end = input.SelectionEnd;

        if (end > start)
        {
            text = text.Remove(start, end - start);
        }

        text = text.Insert(start, value);
        input.Text = text;

        var caret = start + value.Length;
        input.CaretIndex = caret;
        input.SelectionStart = caret;
        input.SelectionEnd = caret;
    }

    private static void BackspaceAtCaret(TextBox input)
    {
        var text = input.Text ?? string.Empty;
        var start = input.SelectionStart;
        var end = input.SelectionEnd;

        if (end > start)
        {
            text = text.Remove(start, end - start);
            input.Text = text;
            input.CaretIndex = start;
        }
        else if (start > 0)
        {
            text = text.Remove(start - 1, 1);
            input.Text = text;
            var caret = start - 1;
            input.CaretIndex = caret;
        }

        input.SelectionStart = input.CaretIndex;
        input.SelectionEnd = input.CaretIndex;
    }

    private static void MoveCaret(TextBox input, int delta)
    {
        var textLength = (input.Text ?? string.Empty).Length;
        var target = input.CaretIndex + delta;
        if (target < 0)
        {
            target = 0;
        }
        else if (target > textLength)
        {
            target = textLength;
        }

        input.CaretIndex = target;
        input.SelectionStart = target;
        input.SelectionEnd = target;
    }

    private async System.Threading.Tasks.Task<string?> ShowUserPasswordDialogAsync()
    {
        var mainWidth = Width;
        var mainHeight = Height;
        var dialogWidth = System.Math.Min(mainWidth * 0.6, 720);
        var dialogHeight = System.Math.Min(mainHeight * 0.3, 260);

        var dialog = new Window
        {
            Title = "User Login",
            Width = dialogWidth,
            Height = dialogHeight,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            ShowInTaskbar = false,
            SystemDecorations = SystemDecorations.None,
            Topmost = true
        };

        var leftOffset = (mainWidth - dialogWidth) / 2;
        if (leftOffset < 0)
        {
            leftOffset = 0;
        }

        var topOffset = mainHeight * 0.15;
        dialog.Position = new PixelPoint(Position.X + (int)leftOffset, Position.Y + (int)topOffset);

        var passwordInput = new TextBox
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            PasswordChar = '\u2022',
            FontSize = 24,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };

        var okButton = new Button
        {
            Content = "Check",
            Width = 90,
            HorizontalAlignment = HorizontalAlignment.Right,
            IsDefault = true
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 90,
            HorizontalAlignment = HorizontalAlignment.Right,
            IsCancel = true
        };

        void Apply()
            => dialog.Close(passwordInput.Text);

        void Cancel()
            => dialog.Close(null);

        okButton.Click += (_, _) =>
        {
            HideKeyboard();
            Apply();
        };

        cancelButton.Click += (_, _) =>
        {
            HideKeyboard();
            Cancel();
        };

        passwordInput.GotFocus += (_, _) => ShowKeyboard(passwordInput, Apply, Cancel);

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { cancelButton, okButton }
        };

        var rootBorder = new Border
        {
            Margin = new Thickness(32, 16),
            Padding = new Thickness(24),
            Background = Avalonia.Media.Brushes.Gainsboro,
            CornerRadius = new CornerRadius(16),
            BorderThickness = new Thickness(2),
            BorderBrush = Avalonia.Media.Brushes.DarkSlateGray
        };

        var content = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto")
        };

        var headerPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center
        };

        var userIcon = new ThemeSvgIcon
        {
            Width = 32,
            Height = 32,
            IconPath = "avares://Amium.Editor/EditorIcons/user.svg"
        };

        var headerText = new TextBlock
        {
            Text = "Login",
            FontSize = 28,
            FontWeight = Avalonia.Media.FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center
        };

        headerPanel.Children.Add(userIcon);
        headerPanel.Children.Add(headerText);

        content.Children.Add(headerPanel);
        Grid.SetRow(headerPanel, 0);

        content.Children.Add(passwordInput);
        Grid.SetRow(passwordInput, 1);

        content.Children.Add(buttonPanel);
        Grid.SetRow(buttonPanel, 3);

        rootBorder.Child = content;
        dialog.Content = rootBorder;

        return await dialog.ShowDialog<string?>(this);
    }

    private async void ChangePassword_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        await ShowChangePasswordDialogAsync(viewModel);
    }

    private async System.Threading.Tasks.Task ShowChangePasswordDialogAsync(MainWindowViewModel viewModel)
    {
        var mainWidth = Width;
        var mainHeight = Height;
        var dialogWidth = System.Math.Min(mainWidth * 0.6, 720);
        var dialogHeight = System.Math.Min(mainHeight * 0.35, 320);

        var dialog = new Window
        {
            Title = "Change Password",
            Width = dialogWidth,
            Height = dialogHeight,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            ShowInTaskbar = false,
            SystemDecorations = SystemDecorations.None,
            Topmost = true
        };

        var leftOffset = (mainWidth - dialogWidth) / 2;
        if (leftOffset < 0)
        {
            leftOffset = 0;
        }

        var topOffset = mainHeight * 0.15;
        dialog.Position = new PixelPoint(Position.X + (int)leftOffset, Position.Y + (int)topOffset);

        var oldPassword = new TextBox
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            PasswordChar = '\u2022'
        };

        var newPassword = new TextBox
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            PasswordChar = '\u2022'
        };

        var repeatPassword = new TextBox
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            PasswordChar = '\u2022'
        };

        var errorText = new TextBlock
        {
            Foreground = Avalonia.Media.Brushes.OrangeRed,
            Margin = new Thickness(0, 6, 0, 0)
        };

        var saveButton = new Button
        {
            Content = "Check",
            Width = 90,
            HorizontalAlignment = HorizontalAlignment.Right,
            IsDefault = true
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 90,
            HorizontalAlignment = HorizontalAlignment.Right,
            IsCancel = true
        };

        void TrySave()
        {
            var oldPwd = oldPassword.Text ?? string.Empty;
            var newPwd = newPassword.Text ?? string.Empty;
            var repeatPwd = repeatPassword.Text ?? string.Empty;

            if (!viewModel.TryChangeCurrentUserPassword(oldPwd, newPwd, repeatPwd, out var error))
            {
                errorText.Text = error;
                return;
            }

            HideKeyboard();
            dialog.Close();
        }

        saveButton.Click += (_, _) => TrySave();

        cancelButton.Click += (_, _) =>
        {
            HideKeyboard();
            dialog.Close();
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { cancelButton, saveButton }
        };

        var rootBorder = new Border
        {
            Margin = new Thickness(32, 16),
            Padding = new Thickness(24),
            Background = Avalonia.Media.Brushes.Gainsboro,
            CornerRadius = new CornerRadius(16),
            BorderThickness = new Thickness(2),
            BorderBrush = Avalonia.Media.Brushes.DarkSlateGray
        };

        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,Auto"),
            ColumnDefinitions = new ColumnDefinitions("Auto,*")
        };

        void AddLabeledRow(int row, string labelText, Control input)
        {
            var label = new TextBlock
            {
                Text = labelText,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 4, 8, 4)
            };

            Grid.SetRow(label, row);
            Grid.SetColumn(label, 0);
            grid.Children.Add(label);

            Grid.SetRow(input, row);
            Grid.SetColumn(input, 1);
            grid.Children.Add(input);
        }

        AddLabeledRow(0, "Old password:", oldPassword);
        AddLabeledRow(1, "New password:", newPassword);
        AddLabeledRow(2, "Repeat password:", repeatPassword);

        void ShowPwKeyboard(TextBox target)
            => ShowKeyboard(target, TrySave, () => dialog.Close());

        oldPassword.GotFocus += (_, _) => ShowPwKeyboard(oldPassword);
        newPassword.GotFocus += (_, _) => ShowPwKeyboard(newPassword);
        repeatPassword.GotFocus += (_, _) => ShowPwKeyboard(repeatPassword);

        Grid.SetRow(errorText, 3);
        Grid.SetColumnSpan(errorText, 2);
        grid.Children.Add(errorText);

        Grid.SetRow(buttonPanel, 5);
        Grid.SetColumnSpan(buttonPanel, 2);
        grid.Children.Add(buttonPanel);

        var headerPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8)
        };

        var userIcon = new ThemeSvgIcon
        {
            Width = 32,
            Height = 32,
            IconPath = "avares://Amium.Editor/EditorIcons/user.svg"
        };

        var headerText = new TextBlock
        {
            Text = "Change Password",
            FontSize = 28,
            FontWeight = Avalonia.Media.FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center
        };

        headerPanel.Children.Add(userIcon);
        headerPanel.Children.Add(headerText);

        var rootGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*")
        };

        rootGrid.Children.Add(headerPanel);
        Grid.SetRow(headerPanel, 0);

        rootGrid.Children.Add(grid);
        Grid.SetRow(grid, 1);

        rootBorder.Child = rootGrid;
        dialog.Content = rootBorder;

        await dialog.ShowDialog(this);
    }

    private void ResetPasswords_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.ResetAllUserPasswords();
    }

    private void Logout_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.TrySetUserByPassword(null);
    }

    private async void NewBook_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = "Select book directory and first page name",
            SuggestedFileName = "Page.json",
            DefaultExtension = "json",
            FileTypeChoices = new[]
            {
                new Avalonia.Platform.Storage.FilePickerFileType("JSON layout")
                {
                    Patterns = new[] { "*.json" }
                }
            }
        });

        if (file is null)
        {
            return;
        }

        var path = file.Path.LocalPath;

        if (!File.Exists(path))
        {
            var content = @"{
    ""Page"": ""Page1"",
    ""Title"": ""New Book"",
    ""Layout"": {
        ""Type"": ""Canvas"",
        ""Children"": []
    }
}";

            File.WriteAllText(path, content);
        }

        viewModel.LoadLayoutFromFile(path);
    }

    private async void LoadBook_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        // Stop all runtime activities before loading a new book.
        Amium.Host.TasksManager.StopAll();
        Amium.Host.ThreadsManager.StopAll();
        Amium.Host.TimerManager.StopAll();

        // Verzeichnis direkt wählen: ein Ordner entspricht einem „Buch“.
        var folders = await StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
        {
            Title = "Select book directory",
            AllowMultiple = false
        });

        if (folders.Count == 0)
        {
            return;
        }

        viewModel.BookProjectPath = folders[0].Path.LocalPath;
        if (viewModel.LoadBookCommand.CanExecute(null))
        {
            viewModel.LoadBookCommand.Execute(null);
        }
    }

    private void SaveLayout_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.SaveCurrentLayout();
    }

    private void SetStartLayout_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.SetCurrentLayoutAsStartup();
    }

    private async void SaveLayoutAs_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        // In directory-book mode, Save As should only copy the book
        // directory – the user chooses a target folder, not a file.
        if (viewModel.IsDirectoryBook)
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
            {
                Title = "Save book as",
                AllowMultiple = false
            });

            if (folders.Count == 0)
            {
                return;
            }

            var targetDirectory = folders[0].Path.LocalPath;
            viewModel.SaveCurrentLayoutAs(targetDirectory);
            return;
        }

        // Single-layout mode keeps the file-based Save As behavior.
        var file = await StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = "Save layout as",
            SuggestedFileName = "Page.json",
            DefaultExtension = "json",
            FileTypeChoices = new[]
            {
                new Avalonia.Platform.Storage.FilePickerFileType("JSON layout")
                {
                    Patterns = new[] { "*.json" }
                }
            }
        });

        if (file is null)
        {
            return;
        }

        var path = file.Path.LocalPath;
        viewModel.SaveCurrentLayoutAs(path);
    }

    private void OnTabPlacementTopClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (viewModel.SetTabStripPlacementCommand.CanExecute("Top"))
        {
            viewModel.SetTabStripPlacementCommand.Execute("Top");
        }
    }

    private void OnTabPlacementRightClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (viewModel.SetTabStripPlacementCommand.CanExecute("Right"))
        {
            viewModel.SetTabStripPlacementCommand.Execute("Right");
        }
    }

    private void OpenLogWindow_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (_logWindow is { } existingWindow)
        {
            if (existingWindow.WindowState == WindowState.Minimized)
            {
                existingWindow.WindowState = WindowState.Normal;
            }

            existingWindow.Activate();
            existingWindow.Topmost = true;
            existingWindow.Topmost = false;
            return;
        }

        viewModel.RefreshLog();
        _logWindow = new LogWindow
        {
            DataContext = viewModel
        };
        _logWindow.Closed += OnLogWindowClosed;
        _logWindow.Show();
        _logWindow.Activate();
    }

    private void ToggleTheme_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.IsDarkTheme = !viewModel.IsDarkTheme;
    }

    private void OnLogWindowClosed(object? sender, EventArgs e)
    {
        if (sender is LogWindow window)
        {
            window.Closed -= OnLogWindowClosed;
        }

        _logWindow = null;
    }

    private void HandleHostUiStateChanged(string action, BookProject? project)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (DataContext is not MainWindowViewModel viewModel)
            {
                return;
            }

            switch (action)
            {
                case "Destroy":
                    viewModel.ApplyDestroyedUi(project);
                    break;
                case "Run" when project is not null:
                    viewModel.ApplyRunningUi(project);
                    break;
            }
        });
    }
}
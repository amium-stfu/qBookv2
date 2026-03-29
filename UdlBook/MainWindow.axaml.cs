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
using Amium.UiEditor.Models;
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

    private async void OnViewsButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not PageModel page)
        {
            return;
        }

        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var menu = new ContextMenu();
        var views = page.Views ?? new System.Collections.Generic.Dictionary<int, string>();

        var orderedViews = views.Count > 0
            ? views.OrderBy(v => v.Key)
            : System.Linq.Enumerable.Empty<System.Collections.Generic.KeyValuePair<int, string>>();

        foreach (var kvp in orderedViews)
        {
            var viewId = kvp.Key;
            var header = string.IsNullOrWhiteSpace(kvp.Value) ? $"View {viewId}" : kvp.Value;

            var item = new MenuItem
            {
                Header = header,
                IsChecked = page.ActualViewId == viewId,
                StaysOpenOnClick = false
            };

            item.Click += (_, _) =>
            {
                page.ActualViewId = viewId;
            };

            menu.Items.Add(item);
        }

        if (viewModel.IsEditMode)
        {
            if (menu.Items.Count > 0)
            {
                menu.Items.Add(new Separator());
            }

            var addItem = new MenuItem
            {
                Header = "Add new view"
            };

            addItem.Click += async (_, _) =>
            {
                var name = await EditorInputDialogs.EditTextAsync(
                    this,
                    header: "Add new view",
                    subHeader: "Enter view name",
                    initialValue: string.Empty);

                if (string.IsNullOrWhiteSpace(name))
                {
                    return;
                }

                var currentViews = page.Views ?? new System.Collections.Generic.Dictionary<int, string>();

                var nextId = currentViews.Count == 0
                    ? 1
                    : currentViews.Keys.Max() + 1;

                currentViews[nextId] = name;
                // Wenn das urspruengliche Dictionary null war, ist CurrentViewCaption ohnehin leer;
                // die eigentliche Persistierung der Views laeuft ueber das Layout-Speichern.
                page.ActualViewId = nextId;
            };

            menu.Items.Add(addItem);
        }

        // Kontextmenü korrekt an den Button anhängen, damit Avalonia eine
        // gültige Visual-Root hat und kein ArgumentNullException wirft.
        menu.PlacementTarget = button;
        button.ContextMenu = menu;

        void Cleanup(object? _, RoutedEventArgs _e)
        {
            menu.Closed -= Cleanup;
            if (ReferenceEquals(button.ContextMenu, menu))
            {
                button.ContextMenu = null;
            }
        }

        menu.Closed += Cleanup;
        menu.Open();
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

    private void OnStartYamlClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        _ = StartYamlDirectoryAsync(viewModel);
    }

    private async System.Threading.Tasks.Task StartYamlDirectoryAsync(MainWindowViewModel viewModel)
    {
        Amium.Host.TasksManager.StopAll();
        Amium.Host.ThreadsManager.StopAll();
        Amium.Host.TimerManager.StopAll();

        var folders = await StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
        {
            Title = "Select YAML book directory",
            AllowMultiple = false
        });

        if (folders.Count == 0)
        {
            return;
        }

        viewModel.LoadYamlBookFromDirectory(folders[0].Path.LocalPath);
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

        var keyboardHeight = Height * 0.25;
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
        var result = await EditorInputDialogs.EditTextAsync(this, "Enter password", "Demo", string.Empty, true);
    }

    private async void OnDemoNumericPasswordClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var result = await EditorInputDialogs.EditNumericAsync(this, "Enter PIN", "Demo", "0", null, true);
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
        // Verwende den neuen, gemeinsamen Text-Dialog im Passwortmodus.
        return await EditorInputDialogs.EditTextAsync(
            this,
            header: "User login",
            subHeader: "Enter password",
            initialValue: string.Empty,
            isPassword: true);
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
        // Verwende hintereinander die neuen Passwort-Dialoge
        // für Alt-Kennwort, neues Kennwort und Wiederholung.

        var oldPassword = await EditorInputDialogs.EditTextAsync(
            this,
            header: "Change password",
            subHeader: "Enter old password",
            initialValue: string.Empty,
            isPassword: true);

        if (oldPassword is null)
        {
            return;
        }

        var newPassword = await EditorInputDialogs.EditTextAsync(
            this,
            header: "Change password",
            subHeader: "Enter new password",
            initialValue: string.Empty,
            isPassword: true);

        if (newPassword is null)
        {
            return;
        }

        var repeatPassword = await EditorInputDialogs.EditTextAsync(
            this,
            header: "Change password",
            subHeader: "Repeat new password",
            initialValue: string.Empty,
            isPassword: true);

        if (repeatPassword is null)
        {
            return;
        }

        if (!viewModel.TryChangeCurrentUserPassword(oldPassword, newPassword, repeatPassword, out var error))
        {
            // Fehlertext kurz anzeigen, ohne eigenen Spezialdialog zu bauen.
            await EditorInputDialogs.EditTextAsync(
                this,
                header: "Change password failed",
                subHeader: error,
                initialValue: string.Empty);
        }
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
            SuggestedFileName = "Page.yaml",
            DefaultExtension = "yaml",
            FileTypeChoices = new[]
            {
                new Avalonia.Platform.Storage.FilePickerFileType("YAML layout")
                {
                    Patterns = new[] { "*.yaml" }
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
            var content = "Page: 'Page1'" + Environment.NewLine
                + "Caption: 'New Book'" + Environment.NewLine
                + "Views:" + Environment.NewLine
                + "  1: 'HomeScreen'" + Environment.NewLine
                + "Controls: []" + Environment.NewLine;

            File.WriteAllText(path, content);
        }

        viewModel.LoadYamlLayoutFromFile(path);
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
            SuggestedFileName = "Page.yaml",
            DefaultExtension = "yaml",
            FileTypeChoices = new[]
            {
                new Avalonia.Platform.Storage.FilePickerFileType("YAML layout")
                {
                    Patterns = new[] { "*.yaml" }
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
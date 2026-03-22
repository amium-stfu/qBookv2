using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Amium.Host;
using Amium.UiEditor.ViewModels;

namespace Amium.UiEditor;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        HostLogger.Initialize();
        HostLogger.Log.Information("Application startup. BaseDirectory={BaseDirectory}", AppContext.BaseDirectory);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Exit += async (_, _) =>
            {
                await Core.ShutdownAsync();
                HostLogger.Shutdown();
            };

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}


using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Amium.Host;
using Amium.Logging;
using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace UdlBook;

public partial class App : Application
{
	private static int _formatExceptionLogCount;
	private static int _globalExceptionHandlersRegistered;

	public override void Initialize() => AvaloniaXamlLoader.Load(this);

	public override void OnFrameworkInitializationCompleted()
	{
		HostLogger.Initialize("UdlBook");
		HostLogger.Log.Information("UdlBook startup. BaseDirectory={BaseDirectory}", AppContext.BaseDirectory);
		RegisterGlobalExceptionHandlers();

		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			desktop.Exit += async (_, _) =>
			{
				await Core.ShutdownAsync();
				HostLogger.Shutdown();
			};

			desktop.MainWindow = new MainWindow
			{
				DataContext = new ViewModels.MainWindowViewModel()
			};
		}

		base.OnFrameworkInitializationCompleted();
	}

	private static void RegisterGlobalExceptionHandlers()
	{
		if (Interlocked.Exchange(ref _globalExceptionHandlersRegistered, 1) != 0)
		{
			return;
		}

		AppDomain.CurrentDomain.FirstChanceException += OnFirstChanceException;
		AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
		TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
		Dispatcher.UIThread.UnhandledException += OnDispatcherUnhandledException;

		HostLogger.Log.Information("Global exception handlers registered");
	}

	private static void OnDispatcherUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
	{
		HostLogger.Log.Fatal(e.Exception, "Unhandled UI thread exception");
	}

	private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
	{
		HostLogger.Log.Error(e.Exception, "Unobserved task exception");
	}

	private static void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
	{
		if (e.ExceptionObject is Exception exception)
		{
			HostLogger.Log.Fatal(exception, "Unhandled AppDomain exception. IsTerminating={IsTerminating}", e.IsTerminating);
			return;
		}

		HostLogger.Log.Fatal("Unhandled AppDomain exception (non-Exception payload). IsTerminating={IsTerminating} Payload={Payload}", e.IsTerminating, e.ExceptionObject);
	}

	private static void OnFirstChanceException(object? sender, FirstChanceExceptionEventArgs e)
	{
		if (e.Exception is not FormatException formatException)
		{
			return;
		}

		var stackTrace = formatException.StackTrace ?? string.Empty;
		if (!stackTrace.Contains("Avalonia", StringComparison.OrdinalIgnoreCase)
			&& !stackTrace.Contains("Amium.UiEditor", StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		var count = Interlocked.Increment(ref _formatExceptionLogCount);
		if (count > 20)
		{
			if (count == 21)
			{
				HostLogger.Log.Warning("Further first-chance FormatException logs suppressed after {Count} entries", count - 1);
			}

			return;
		}

		HostLogger.Log.Warning(formatException, "First-chance FormatException #{Count}: {Message}", count, formatException.Message);
	}
}

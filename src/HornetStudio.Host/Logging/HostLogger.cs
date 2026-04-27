using Serilog;
using Serilog.Events;

namespace HornetStudio.Logging;

public static class HostLogger
{
    private static bool _initialized;
    private static string _applicationName = "HornetStudio";

    public static ILogger Log { get; private set; } = Serilog.Log.Logger;
    public static ProcessLog ProcessLog { get; } = new();

    public static string ApplicationName => _applicationName;
    public static string LogDirectory => Path.Combine(AppContext.BaseDirectory, "logs");
    public static string LogFilePath => Path.Combine(LogDirectory, "host-.log");
    public static string CurrentLogFilePath => Path.Combine(LogDirectory, $"host-{DateTime.Now:yyyyMMdd}.log");

    public static void Initialize(string applicationName = "HornetStudio")
    {
        if (_initialized)
        {
            return;
        }

        _applicationName = string.IsNullOrWhiteSpace(applicationName) ? "HornetStudio" : applicationName.Trim();

        Directory.CreateDirectory(LogDirectory);
        ProcessLog.SetLogDirectory(LogDirectory);

        Log = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.WithProperty("App", _applicationName)
            .WriteTo.Debug(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.Sink(new ProcessLogSink(ProcessLog))
            .WriteTo.File(
                LogFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Serilog.Log.Logger = Log;
        _initialized = true;
        Log.Information("Logger initialized. App={AppName} LogDirectory={LogDirectory} CurrentLogFile={CurrentLogFile}", _applicationName, LogDirectory, CurrentLogFilePath);
    }

    public static void Shutdown()
    {
        if (!_initialized)
        {
            return;
        }

        Log.Information("Logger shutdown. App={AppName}", _applicationName);
        Serilog.Log.CloseAndFlush();
        _initialized = false;
    }
}
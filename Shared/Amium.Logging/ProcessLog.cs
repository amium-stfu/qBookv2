using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.Data;
using System.Diagnostics;

namespace Amium.Logging;

public sealed class ProcessLog
{
    private readonly object _bufferLock = new();
    private readonly DataTable _bufferTable = CreateBufferTable();
    private ILogger? _log;
    private bool _showDebug = true;
    private bool _showInfo = true;
    private bool _showWarning = true;
    private bool _showError = true;
    private bool _showFatal = true;
    private bool _pause;
    private string? _logDirectory;

    private const int MaxBufferedRows = 1000;

    public ILogger Log => _log ?? throw new InvalidOperationException("ProcessLog not initialized. Call InitializeLog(...) first.");
    public event Action? DisplaySettingsChanged;
    public event Action<ProcessLogEntry>? EntryAdded;

    public bool ShowDebug
    {
        get => _showDebug;
        set
        {
            if (_showDebug == value) return;
            _showDebug = value;
            OnDisplaySettingsChanged();
        }
    }

    public bool ShowInfo
    {
        get => _showInfo;
        set
        {
            if (_showInfo == value) return;
            _showInfo = value;
            OnDisplaySettingsChanged();
        }
    }

    public bool ShowWarning
    {
        get => _showWarning;
        set
        {
            if (_showWarning == value) return;
            _showWarning = value;
            OnDisplaySettingsChanged();
        }
    }

    public bool ShowError
    {
        get => _showError;
        set
        {
            if (_showError == value) return;
            _showError = value;
            OnDisplaySettingsChanged();
        }
    }

    public bool ShowFatal
    {
        get => _showFatal;
        set
        {
            if (_showFatal == value) return;
            _showFatal = value;
            OnDisplaySettingsChanged();
        }
    }

    public bool Pause
    {
        get => _pause;
        set
        {
            if (_pause == value) return;
            _pause = value;
            OnDisplaySettingsChanged();
        }
    }

    public string? LogDirectory => _logDirectory;

    public void InitializeLog(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        SetLogDirectory(directory);

        var logFilePath = Path.Combine(directory, "process-.log");
        _log = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                path: logFilePath,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.Sink(new ProcessLogSink(this))
            .CreateLogger();
    }

    public void SetLogDirectory(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _logDirectory = directory;
    }

    public void OpenLogDirectory()
    {
        if (string.IsNullOrWhiteSpace(_logDirectory))
        {
            return;
        }

        Directory.CreateDirectory(_logDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = _logDirectory,
            UseShellExecute = true
        });
    }

    public void Error(string message, Exception ex) => Log.Error(ex, message);
    public void Fatal(string message, Exception ex) => Log.Fatal(ex, message);
    public void Info(string message) => Log.Information(message);
    public void Debug(string message) => Log.Debug(message);

    public DataTable GetBufferedLogs(string? levelFilter = null, string? textFilter = null)
    {
        lock (_bufferLock)
        {
            var result = _bufferTable.Clone();

            foreach (DataRow row in _bufferTable.Rows)
            {
                var level = row["Level"]?.ToString() ?? string.Empty;
                var message = row["Message"]?.ToString() ?? string.Empty;

                if (TryParseLevel(level, out var parsedLevel) && !IsLevelVisible(parsedLevel))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(levelFilter) && !string.Equals(level, levelFilter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(textFilter) && message.IndexOf(textFilter, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                result.ImportRow(row);
            }

            return result;
        }
    }

    public IReadOnlyList<ProcessLogEntry> GetEntries(string? levelFilter = null, string? textFilter = null)
    {
        var table = GetBufferedLogs(levelFilter, textFilter);
        return table.Rows
            .Cast<DataRow>()
            .Select(row => new ProcessLogEntry(
                row["Timestamp"] is DateTime timestamp ? timestamp : DateTime.MinValue,
                row["Level"]?.ToString() ?? string.Empty,
                row["Message"]?.ToString() ?? string.Empty,
                row["Exception"]?.ToString() ?? string.Empty))
            .ToList();
    }

    public bool IsLevelVisible(LogEventLevel level)
    {
        return level switch
        {
            LogEventLevel.Verbose => true,
            LogEventLevel.Debug => ShowDebug,
            LogEventLevel.Information => ShowInfo,
            LogEventLevel.Warning => ShowWarning,
            LogEventLevel.Error => ShowError,
            LogEventLevel.Fatal => ShowFatal,
            _ => true
        };
    }

    internal void AddBufferedEntry(LogEvent logEvent, string message)
    {
        var entry = new ProcessLogEntry(
            logEvent.Timestamp.LocalDateTime,
            logEvent.Level.ToString(),
            message,
            logEvent.Exception?.ToString() ?? string.Empty);

        lock (_bufferLock)
        {
            _bufferTable.Rows.Add(entry.Timestamp, entry.Level, entry.Message, entry.Exception);

            while (_bufferTable.Rows.Count > MaxBufferedRows)
            {
                _bufferTable.Rows.RemoveAt(0);
            }
        }

        if (!Pause && IsLevelVisible(logEvent.Level))
        {
            EntryAdded?.Invoke(entry);
        }
    }

    private void OnDisplaySettingsChanged()
    {
        DisplaySettingsChanged?.Invoke();
    }

    private static bool TryParseLevel(string level, out LogEventLevel parsedLevel)
    {
        return Enum.TryParse(level, true, out parsedLevel);
    }

    private static DataTable CreateBufferTable()
    {
        var table = new DataTable("ProcessLogBuffer");
        table.Columns.Add("Timestamp", typeof(DateTime));
        table.Columns.Add("Level", typeof(string));
        table.Columns.Add("Message", typeof(string));
        table.Columns.Add("Exception", typeof(string));
        return table;
    }
}

public sealed record ProcessLogEntry(DateTime Timestamp, string Level, string Message, string Exception);

public sealed class ProcessLogSink : ILogEventSink
{
    private readonly ProcessLog _processLog;
    private readonly IFormatProvider? _formatProvider;

    public ProcessLogSink(ProcessLog processLog, IFormatProvider? formatProvider = null)
    {
        _processLog = processLog;
        _formatProvider = formatProvider;
    }

    public void Emit(LogEvent logEvent)
    {
        var message = logEvent.RenderMessage(_formatProvider);
        _processLog.AddBufferedEntry(logEvent, message);
    }
}
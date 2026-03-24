using Serilog;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Amium.Logging;

public sealed class CsvLogger
{
    public sealed class LogObject
    {
        public LogObject(string name, string unit, string format, Func<object?> value, string type)
        {
            Name = name;
            Unit = unit;
            Format = format;
            ValueType = type;
            GetLogObject = value;
            Object = GetLogObject();
        }

        public string Name { get; set; }
        public string Unit { get; set; }
        public string Format { get; set; }
        public string ValueType { get; set; }
        public Func<object?> GetLogObject { get; set; }
        public object? Object { get; }
        public object? CurrentValue => GetLogObject();
    }

    public List<LogObject> LogList { get; } = new();
    public ConcurrentQueue<string> Lines { get; } = new();

    private DateTime _start;
    private CancellationTokenSource? _cts;
    private Task? _loggingTask;
    private Task? _writingTask;
    private StreamWriter? _writer;

    public string Filename { get; set; } = "default.csv";
    public string Separator { get; set; } = ";";
    public string DecimalSeparator { get; set; } = ".";
    public string Directory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AmiumLogs");
    public int Interval { get; set; } = 1000;
    public bool Running { get; private set; }
    public TimeSpan TimeRelative { get; private set; }
    public string Name { get; }
    public string FullPath => Path.Combine(Directory, Filename);

    public CsvLogger(string name)
    {
        Name = name;
    }

    public void Add(string name, string unit, string format, Func<object?> value)
    {
        var result = value();
        var type = DetectValueType(result);
        LogList.Add(new LogObject(name, unit, format, value, type));
    }

    public void Reset()
    {
        LogList.Clear();
    }

    public void Init(string? file = null)
    {
        if (!string.IsNullOrWhiteSpace(file))
        {
            Directory = Path.GetDirectoryName(file!) ?? Directory;
            Filename = Path.GetFileName(file!);
        }
        else
        {
            Filename = $"{DateTime.Now:yyyy-MM-dd HH.mm.ss}_{Name}.csv";
        }

        System.IO.Directory.CreateDirectory(Directory);

        using var writer = new StreamWriter(FullPath, append: false, encoding: Encoding.UTF8);
        writer.WriteLine(BuildHeaderLine());
        writer.WriteLine(BuildUnitLine());
    }

    public void Start(int interval = -1)
    {
        Init();
        StartInternal(interval);
    }

    public void Start(string file, int interval = -1)
    {
        Init(file);
        StartInternal(interval);
    }

    public async Task StopAsync()
    {
        Running = false;

        var localCts = _cts;
        if (localCts is null)
        {
            return;
        }

        localCts.Cancel();

        try
        {
            await Task.WhenAll(
                _loggingTask ?? Task.CompletedTask,
                _writingTask ?? Task.CompletedTask).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            try
            {
                if (_writer is not null)
                {
                    await _writer.FlushAsync().ConfigureAwait(false);
                    _writer.Close();
                }
            }
            catch
            {
            }

            _writer = null;
            localCts.Dispose();
            _cts = null;
            _loggingTask = null;
            _writingTask = null;
        }
    }

    public void Destroy()
    {
        _cts?.Cancel();
        Running = false;
    }

    public bool WriterIsOpen() => _writer is not null;

    public void OpenFolder()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = Directory,
            UseShellExecute = true
        });
    }

    public string GetValues()
    {
        try
        {
            var builder = new StringBuilder();
            foreach (var item in LogList)
            {
                builder.Append(FormatValue(item.CurrentValue, item.Format)).Append(Separator);
            }

            if (builder.Length > 0)
            {
                builder.Length--;
            }

            return builder.ToString();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[CsvLogger] Failed to collect values.");
            return "Error";
        }
    }

    private void StartInternal(int interval)
    {
        if (Running)
        {
            return;
        }

        if (interval > 0)
        {
            Interval = interval;
        }

        _cts = new CancellationTokenSource();
        _start = DateTime.Now;
        Running = true;

        var token = _cts.Token;
        _loggingTask = Task.Run(() => RunLogger(token), token);
        _writingTask = Task.Run(() => WriteLogsToFileAsync(token), token);
        Log.Information("[CsvLogger] Started '{Name}' file={FullPath} interval={Interval}ms", Name, FullPath, Interval);
    }

    private string BuildHeaderLine()
    {
        var builder = new StringBuilder();
        builder.Append("datetime").Append(Separator);
        builder.Append("timeRel").Append(Separator);
        foreach (var obj in LogList)
        {
            builder.Append(obj.Name).Append(Separator);
        }

        if (builder.Length > 0)
        {
            builder.Length--;
        }

        return builder.ToString();
    }

    private string BuildUnitLine()
    {
        var builder = new StringBuilder();
        builder.Append(Separator);
        builder.Append("s").Append(Separator);
        foreach (var obj in LogList)
        {
            builder.Append(obj.Unit).Append(Separator);
        }

        if (builder.Length > 0)
        {
            builder.Length--;
        }

        return builder.ToString();
    }

    private static string DetectValueType(object? value)
    {
        if (value is null)
        {
            return "word";
        }

        var valueType = Nullable.GetUnderlyingType(value.GetType()) ?? value.GetType();
        var typeCode = Type.GetTypeCode(valueType);

        return typeCode switch
        {
            TypeCode.Single or TypeCode.Double or TypeCode.Decimal => "float",
            TypeCode.SByte or TypeCode.Byte or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 => "int",
            TypeCode.DateTime => "timestamp",
            _ => "word"
        };
    }

    private string FormatValue(object? value, string format)
    {
        if (value is null)
        {
            return string.Empty;
        }

        var hasFormat = !string.IsNullOrWhiteSpace(format);

        return value switch
        {
            double d => ApplyDecimalSeparator(hasFormat ? d.ToString(format, CultureInfo.InvariantCulture) : d.ToString(CultureInfo.InvariantCulture)),
            float f => ApplyDecimalSeparator(hasFormat ? f.ToString(format, CultureInfo.InvariantCulture) : f.ToString(CultureInfo.InvariantCulture)),
            decimal m => ApplyDecimalSeparator(hasFormat ? m.ToString(format, CultureInfo.InvariantCulture) : m.ToString(CultureInfo.InvariantCulture)),
            DateTime dt => hasFormat ? dt.ToString(format, CultureInfo.InvariantCulture) : dt.ToString(CultureInfo.InvariantCulture),
            IFormattable formattable when hasFormat => formattable.ToString(format, CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };
    }

    private string ApplyDecimalSeparator(string value)
    {
        if (DecimalSeparator == ".")
        {
            return value;
        }

        return value.Replace(".", DecimalSeparator, StringComparison.Ordinal);
    }

    private void RunLogger(CancellationToken token)
    {
        var effectiveInterval = Interval > 0 ? Interval : 1;
        var intervalTicks = effectiveInterval * (Stopwatch.Frequency / 1000.0);
        var nextTick = Stopwatch.GetTimestamp();

        while (!token.IsCancellationRequested)
        {
            var nowTick = Stopwatch.GetTimestamp();
            var remainingTicks = nextTick - nowTick;

            if (remainingTicks > 0)
            {
                while (remainingTicks > Stopwatch.Frequency / 1000 && !token.IsCancellationRequested)
                {
                    Thread.Sleep(1);
                    nowTick = Stopwatch.GetTimestamp();
                    remainingTicks = nextTick - nowTick;
                }

                while (Stopwatch.GetTimestamp() < nextTick && !token.IsCancellationRequested)
                {
                    Thread.SpinWait(50);
                }
            }

            if (token.IsCancellationRequested)
            {
                break;
            }

            var now = DateTime.Now;
            TimeRelative = now - _start;
            var timestamp = now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
            var timeRel = TimeRelative.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture);
            var values = GetValues();

            Lines.Enqueue(string.IsNullOrEmpty(values)
                ? $"{timestamp}{Separator}{timeRel}"
                : $"{timestamp}{Separator}{timeRel}{Separator}{values}");

            nextTick += (long)intervalTicks;

            nowTick = Stopwatch.GetTimestamp();
            if (nowTick > nextTick + (long)intervalTicks)
            {
                nextTick = nowTick + (long)intervalTicks;
            }
        }
    }

    private async Task WriteLogsToFileAsync(CancellationToken token)
    {
        System.IO.Directory.CreateDirectory(Directory);
        _writer = new StreamWriter(FullPath, append: true, encoding: Encoding.UTF8);

        try
        {
            while (!token.IsCancellationRequested)
            {
                if (!Lines.IsEmpty)
                {
                    while (Lines.TryDequeue(out var line))
                    {
                        await _writer.WriteLineAsync(line).ConfigureAwait(false);
                    }

                    await _writer.FlushAsync().ConfigureAwait(false);
                }

                await Task.Delay(50, token).ConfigureAwait(false);
            }
        }
        finally
        {
            while (Lines.TryDequeue(out var line))
            {
                await _writer.WriteLineAsync(line).ConfigureAwait(false);
            }

            await _writer.FlushAsync().ConfigureAwait(false);
            _writer.Close();
        }
    }
}
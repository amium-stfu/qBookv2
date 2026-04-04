using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using Amium.Host;
using Amium.Items;
using Amium.Contracts;


namespace Amium.Logging
{
    public class CsvLogger
    {
        public class LogObject
        {
            public string Name { get; set; }
            public string Unit { get; set; }
            public string Format { get; set; }
            public string ValueType { get; set; }
            public string Caption { get; set; }

            public Func<object> GetLogObject { get; set; } // Delegate for real-time value access

            public object Object;

            public LogObject(string name, string unit, string format, Func<object> value, string type, string caption = "")
            {
                GetLogObject = value;
                Name = name;
                Unit = unit;
                Format = format;
                ValueType = type;
                Caption = caption ?? string.Empty;

                Object = GetLogObject();
            }

            public object CurrentValue => GetLogObject();

        }

        public List<LogObject> LogList = new List<LogObject>();
        public ConcurrentQueue<string> Lines = new ConcurrentQueue<string>();
        private DateTime start;
        private ATask loggingTask;
        private ATask writingTask;

        private StreamWriter myWriter;

        public string Filename = "default";
        public string Seperator = ";";
        public string DecimalSeperator = ".";
        public string Directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AmiumLogs");

        public int Interval = 1000;
        public bool Running = false;

        public TimeSpan TimeRelative;
        public string Name { get; set; }

        public CsvLogger(string name)
        {
            Name = name;

        }

        /// <summary>
        /// Interal default = 1000
        /// </summary>
        /// <param name="interval"></param>
        /// <param name="filename"></param>
        public void Start(int interval = -1)
        {
            Init();
            Running = true;
            if (interval > 0)
                Interval = interval;
            start = DateTime.Now;

            loggingTask = new ATask($"CsvLogger-Run-{Name}", RunLogger);
            writingTask = new ATask($"CsvLogger-Write-{Name}", WriteLogsToFile);

        }

        public void Start(string file, int interval = -1)
        {
            Init(file);
            Running = true;
            if (interval > 0)
                Interval = interval;
            start = DateTime.Now;

            loggingTask = new ATask($"CsvLogger-Run-{Name}", RunLogger);
            writingTask = new ATask($"CsvLogger-Write-{Name}", WriteLogsToFile);

        }

        public void Add(string name, string unit, string format, Func<object> value, string caption = "")
        {
            object result = value();
            string type = DetectValueType(result);

            LogList.Add(new LogObject(name, unit, format, value, type, caption));
        }

        public void AddSignal(ISignal signal, string formatOverride = "", string caption = "", string unitOverride = "")
        {
            if (signal == null)
            {
                throw new ArgumentNullException(nameof(signal));
            }

            var descriptor = signal.Descriptor;
            var name = descriptor.Name;
            var unit = string.IsNullOrWhiteSpace(unitOverride)
                ? descriptor.Unit ?? string.Empty
                : unitOverride;

            var format = string.IsNullOrWhiteSpace(formatOverride)
                ? descriptor.Format ?? string.Empty
                : formatOverride;

            object sampleValue;
            try
            {
                sampleValue = signal.Value!;
            }
            catch
            {
                sampleValue = string.Empty;
            }

            var valueType = DetectValueType(sampleValue);

            LogList.Add(new LogObject(name, unit, format, () => signal.Value!, valueType, caption));
        }

        public void AddItem(Item item, string format = "", string caption = "", string unitOverride = "")
        {
            string unit = string.IsNullOrWhiteSpace(unitOverride) ? string.Empty : unitOverride;

            Func<object> valueGetter = () => item.Params["Value"].Value;
            string type = DetectValueType(valueGetter());

            if (item.Params.Has("Format"))
            {
                format = item.GetParamter("Format");
            }

            if (item.Params.Has("Unit") && string.IsNullOrWhiteSpace(unit))
            {
                unit = item.GetParamter("Unit");
            }
            Debug.WriteLine($"Adding log for item '{item.Name}' with type '{type}' and format '{format}' caption='{caption}'");


            LogList.Add(new LogObject(name: item.Name, unit: unit, format: format, value: valueGetter, type: type, caption: caption));
        }



        public void Reset()
        {
            LogList.Clear();
        }

        public async Task Stop()
        {
            Running = false;
            var logTask = loggingTask;
            var writeTask = writingTask;

            loggingTask = null;
            writingTask = null;

            var waitTasks = new List<Task>();

            if (logTask != null)
            {
                try { logTask.Stop(); } catch { }
                waitTasks.Add(logTask.AwaitAsync());
            }

            if (writeTask != null)
            {
                try { writeTask.Stop(); } catch { }
                waitTasks.Add(writeTask.AwaitAsync());
            }

            if (waitTasks.Count > 0)
            {
                try
                {
                    await Task.WhenAll(waitTasks).ConfigureAwait(false);
                }
                catch
                {
                    // ignore exceptions during stopping
                }
            }

            // Ensure writer is closed even if task faulted before closing it.
            try
            {
                myWriter?.Flush();
                myWriter?.Close();
            }
            catch
            {
                // ignore
            }

            myWriter = null;
        }

        public bool WriterIsOpen()
        {
            return myWriter != null;
        }

        public void Init(string file = null)
        {
            if (file != null)
            {
                Directory = Path.GetDirectoryName(file) ?? Directory;
                Filename = Path.GetFileName(file);
            }
            else
            {
                Filename = DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss") + "_" + Name + ".csv";
            }

            System.IO.Directory.CreateDirectory(Directory);

            using var writer = new StreamWriter(Path.Combine(Directory, Filename), append: true, encoding: Encoding.UTF8);

            static string Quote(string value) => "\"" + (value?.Replace("\"", "\"\"") ?? string.Empty) + "\"";

            // Zeile 1: "datetime";"timeRel";{Signal.Name};{Signal.Name};..
            var headerNames = new StringBuilder();
            headerNames.Append(Quote("datetime")).Append(Seperator);
            headerNames.Append(Quote("timeRel"));
            foreach (var obj in LogList)
            {
                headerNames.Append(Seperator).Append(Quote(obj.Name));
            }
            writer.WriteLine(headerNames.ToString());

            // Zeile 2: "DateTime";"Time relativ";{Signal.Header.Caption};{Signal.Header.Caption};..
            var headerCaptions = new StringBuilder();
            headerCaptions.Append(Quote("DateTime")).Append(Seperator);
            headerCaptions.Append(Quote("Time relativ"));
            foreach (var obj in LogList)
            {
                var caption = string.IsNullOrWhiteSpace(obj.Caption) ? obj.Name : obj.Caption;
                headerCaptions.Append(Seperator).Append(Quote(caption));
            }
            writer.WriteLine(headerCaptions.ToString());

            // Zeile 3: "";"s";{Signal.Unit};{Signal.Unit};..
            var headerUnits = new StringBuilder();
            headerUnits.Append(Quote(string.Empty)).Append(Seperator);
            headerUnits.Append(Quote("s"));
            foreach (var obj in LogList)
            {
                headerUnits.Append(Seperator).Append(Quote(obj.Unit));
            }
            writer.WriteLine(headerUnits.ToString());
        }
        public void OpenFolder()
        {
            System.Diagnostics.Process.Start("explorer.exe", Directory);
        }
        public virtual string getValues()
        {
            try
            {
                var stringBuilder = new StringBuilder();

                foreach (var item in LogList)
                {
                    object currentValue = item.CurrentValue;
                    stringBuilder.Append(FormatValue(currentValue, item.Format)).Append(Seperator);
                }
                // Remove the last comma
                if (stringBuilder.Length > 0)
                {
                    stringBuilder.Length--;
                }
                return stringBuilder.ToString();
            }
            catch
            {
                return "Error";
            }

        }

        private static string DetectValueType(object value)
        {
            if (value == null)
                return "word";

            Type valueType = Nullable.GetUnderlyingType(value.GetType()) ?? value.GetType();
            TypeCode typeCode = Type.GetTypeCode(valueType);

            return typeCode switch
            {
                TypeCode.Single or TypeCode.Double or TypeCode.Decimal => "float",
                TypeCode.SByte or TypeCode.Byte or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 => "int",
                TypeCode.DateTime => "timestamp",
                _ => "word"
            };
        }

        private string FormatValue(object value, string format)
        {
            if (value == null)
                return string.Empty;

            bool hasFormat = !string.IsNullOrWhiteSpace(format);

            return value switch
            {
                double d => ApplyDecimalSeparator(hasFormat ? d.ToString(format, CultureInfo.InvariantCulture) : d.ToString(CultureInfo.InvariantCulture)),
                float f => ApplyDecimalSeparator(hasFormat ? f.ToString(format, CultureInfo.InvariantCulture) : f.ToString(CultureInfo.InvariantCulture)),
                decimal m => ApplyDecimalSeparator(hasFormat ? m.ToString(format, CultureInfo.InvariantCulture) : m.ToString(CultureInfo.InvariantCulture)),
                DateTime dt => ApplyDecimalSeparator(hasFormat ? dt.ToString(format, CultureInfo.InvariantCulture) : dt.ToString(CultureInfo.InvariantCulture)),
                IFormattable formattable when hasFormat => formattable.ToString(format, CultureInfo.InvariantCulture),
                _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
            };
        }

        private string ApplyDecimalSeparator(string value)
        {
            if (DecimalSeperator == ".")
                return value;

            return value.Replace(".", DecimalSeperator);
        }

        private async Task RunLogger(System.Threading.CancellationToken token)
        {
            int effectiveInterval = Interval > 0 ? Interval : 1;
            var stopwatch = Stopwatch.StartNew();
            long nextMs = 0;

            while (!token.IsCancellationRequested)
            {
                long nowMs = stopwatch.ElapsedMilliseconds;
                long delayMs = nextMs - nowMs;

                if (delayMs > 0)
                {
                    try
                    {
                        await Task.Delay((int)delayMs, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }

                if (token.IsCancellationRequested)
                    break;

                DateTime now = DateTime.Now;
                TimeRelative = now - start;
                string timestamp = now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
                string timeRel = TimeRelative.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture).Replace(".", DecimalSeperator);
                string values = getValues();

                // Always write value columns when signals are configured, even if
                // individual values are currently empty. This keeps the CSV
                // column count consistent with the header.
                if (LogList.Count == 0)
                    Lines.Enqueue($"{timestamp}{Seperator}{timeRel}");
                else
                    Lines.Enqueue($"{timestamp}{Seperator}{timeRel}{Seperator}{values}");

                nextMs += effectiveInterval;

                // If we are far behind (e.g. due to pauses), resync to avoid huge catch-up loops.
                nowMs = stopwatch.ElapsedMilliseconds;
                if (nowMs > nextMs + effectiveInterval)
                {
                    nextMs = nowMs + effectiveInterval;
                }
            }
        }

        private async Task WriteLogsToFile(System.Threading.CancellationToken token)
        {
            System.IO.Directory.CreateDirectory(Directory);
            myWriter = new StreamWriter(Path.Combine(Directory, Filename), append: true, encoding: Encoding.UTF8);

            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (!Lines.IsEmpty)
                    {
                        while (Lines.TryDequeue(out string result))
                        {
                            await myWriter.WriteLineAsync(result);
                        }
                        await myWriter.FlushAsync();
                    }
                    await Task.Delay(50, token); // Adjust delay for batch writing
                }
            }
            finally
            {
                while (Lines.TryDequeue(out string result))
                {
                    await myWriter.WriteLineAsync(result);
                }

                await myWriter.FlushAsync();
                myWriter.Close();
            }
        }

        public void Destroy()
        {
            Running = false;

            try { loggingTask?.Stop(); } catch { }
            try { writingTask?.Stop(); } catch { }

            loggingTask = null;
            writingTask = null;
        }
    }


}

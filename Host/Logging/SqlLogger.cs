using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Globalization;
using System.Xml.Linq;
using System.Threading.Tasks;
using System.Data.SQLite;
using Amium.Host;

namespace Amium.Logging
{
    public class SqlLogger
    {
        public class LogObject
        {
            public string Name { get; set; }
            public string Unit { get; set; }
            public string Description { get; set; }
            public string Format { get; set; }
            public string ValueType { get; set; }

            public string SqlTable { get; set; }
            public Func<object> GetLogObject { get; set; } // Delegate for real-time value access

            public object Object;



            public LogObject(string name, string unit, string format, Func<object> value, string type, string sqlTable = "", string description = "")
            {
                GetLogObject = value;
                Name = name;
                Unit = unit;
                Format = format;
                ValueType = type;
                SqlTable = sqlTable;
                Description = description;
                Object = GetLogObject();
            }

            public object CurrentValue => GetLogObject();

        }

        Dictionary<string, LogObject> logList = new Dictionary<string, LogObject>();
        /// <summary>
        /// A thread-safe queue that holds the SQL commands to be written to the database.
        /// </summary>
        public ConcurrentQueue<string> Lines = new ConcurrentQueue<string>();
        /// <summary>
        /// Gets the relative time elapsed since the logger started.
        /// </summary>
        public TimeSpan timeRel;
        /// <summary>
        /// Gets the exact date and time when the logger was started.
        /// </summary>
        public DateTime start;
        /// <summary>
        /// Gets a value indicating whether the logger is currently running.
        /// </summary>
        public bool Running = false;
        bool initDb = false;

        string connectionString = string.Empty;

        public string Directory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AmiumLogs");

        /// <summary>
        /// Gets or sets the path to the database file. If set to "default", a new file is created with a timestamp.
        /// </summary>
        public string File = "default";

        private readonly List<ATask> loggingTasks = new();
        private ATask? writingTask;

        protected Dictionary<string, int> loggers = new Dictionary<string, int>();
        public string Name { get; set; } = "SqlLogger";

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlLogger"/> class with a specified name.
        /// </summary>
        /// <param name="name">The name of the logger instance.</param>
        public SqlLogger(string name)
        {
            this.Name = name;

        }
        /// <summary>
        /// Adds a data point to be logged periodically.
        /// </summary>
        /// <param name="name">The unique name for the log value (used as a column name).</param>
        /// <param name="text">A descriptive text for the value.</param>
        /// <param name="unit">The unit of the value.</param>
        /// <param name="format">The string format for the value.</param>
        /// <param name="period">The logging interval in milliseconds.</param>
        /// <param name="value">A function that returns the value to be logged.</param>
        public void Add(string name, string text, string unit, string format, int period, Func<object> value)
        {
            object result = value();
            string type = DetectSqlValueType(result);

            string tbl = "p" + period;
            if (!loggers.ContainsKey(tbl))
            {
                loggers.Add(tbl, period);
                Console.WriteLine(tbl);
            }
            if (!logList.ContainsKey(name))
                logList.Add(name, new LogObject(name, unit, format, value, type, tbl, text));
            else
                Debug.WriteLine($"SQLlogger  already contains Key: '" + name + "'");
        }

        /// <summary>
        /// Initializes the database. This includes creating the DB file and setting up tables.
        /// <returns><c>true</c> if initialization is successful; otherwise, <c>false</c>.</returns>
        public bool Init()
        {
            initDb = true;
            if (File == "default")
            {
                System.IO.Directory.CreateDirectory(Directory);
                string dbFile = System.IO.Path.Combine(Directory, DateTime.Now.ToString("yyyy-MM-dd_HH.mm.ss") + "_" + Name + ".db");
                SQLiteConnection.CreateFile(dbFile);
                connectionString = $"Data Source={dbFile};Version=3;";
            }
            else
            {
                var directory = System.IO.Path.GetDirectoryName(File);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }

                SQLiteConnection.CreateFile(File);
                connectionString = $"Data Source={File};Version=3;";
            }

            string cmd = "";
            using (var database = new SQLiteConnection(connectionString))
            {
                try
                {
                    database.Open();
                    cmd = @"
                     DROP TABLE IF EXISTS valueData;
                     CREATE TABLE valueData (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        name TEXT,
                        description TEXT,
                        unit TEXT,
                        valueType TEXT,
                        format TEXT,
                        sqlTable TEXT
                    );";
                    SQLiteCommand command = new SQLiteCommand(cmd, database);
                    command.ExecuteNonQuery();

                    Dictionary<string, List<string>> tables = new Dictionary<string, List<string>>();
                    foreach (var i in logList)
                    {
                        cmd = "INSERT INTO valueData (name, description, unit, valueType, format, sqlTable) VALUES (@name, @description, @unit, @valueType, @format, @sqlTable);";
                        command = new SQLiteCommand(cmd, database);
                        command.Parameters.AddWithValue("@name", i.Value.Name ?? string.Empty);
                        command.Parameters.AddWithValue("@description", i.Value.Description ?? string.Empty);
                        command.Parameters.AddWithValue("@unit", i.Value.Unit ?? string.Empty);
                        command.Parameters.AddWithValue("@valueType", i.Value.ValueType ?? string.Empty);
                        command.Parameters.AddWithValue("@format", i.Value.Format ?? string.Empty);
                        command.Parameters.AddWithValue("@sqlTable", i.Value.SqlTable ?? string.Empty);
                        command.ExecuteNonQuery();

                        var sqlTable = i.Value.SqlTable ?? string.Empty;
                        if (!tables.ContainsKey(sqlTable))
                        {
                            tables.Add(sqlTable, new List<string>()
                                { i.Value.Name + " " + i.Value.ValueType });
                        }
                        else
                        {
                            tables[sqlTable].Add(i.Value.Name + " " + i.Value.ValueType);
                        }
                    }

                    foreach (var entry in tables)
                    {
                        string tbl = entry.Key;
                        string values = string.Join(", ", entry.Value);
                        cmd = $"DROP TABLE IF EXISTS {tbl};  CREATE TABLE {tbl}(id INTEGER PRIMARY KEY AUTOINCREMENT,datetime TEXT, timeRel REAL, {values});";
                        command = new SQLiteCommand(cmd, database);
                        command.ExecuteNonQuery();
                    }
                    database.Close();
                }

                catch (SQLiteException ex)
                {
                    Debug.WriteLine($"{Name}.SqlLogger SQLite error on init: {ex.Message}");
                    database.Close();
                    return false;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"{Name}.SqlLogger general error on init: {ex.Message}");
                    database.Close();
                    return false;
                }

                return true;

            }
        }
        /// <summary>
        /// Resets the initialization status of the database, forcing a re-initialization on the next <see cref="Start"/>.
        /// </summary>
        public void Reset()
        {
            initDb = false;
        }
        /// <summary>
        /// Starts the logging process by initiating background tasks for data collection and writing.
        /// </summary>
        public void Start()
        {

            if (!initDb)
            {
                if (!Init())
                    return;
            }

            Running = true;

            loggingTasks.Clear();
            start = DateTime.Now;

            foreach (var item in loggers)
            {
                string tbl = item.Key;
                int interval = item.Value;
                var loggingTask = new ATask($"SqlLogger-Run-{Name}-{tbl}", token => Task.Run(() => RunLogger(token, interval, tbl), token));
                loggingTasks.Add(loggingTask);

            }

            writingTask = new ATask($"SqlLogger-Write-{Name}", WriteLogsToFile);
        }
        /// <summary>
        /// Stops the logging process gracefully and waits for all pending data to be written to the database.
        /// </summary>
        /// <returns>A task that represents the asynchronous stop operation.</returns>
        public async Task Stop()
        {
            Running = false;

            var loggingSnapshot = loggingTasks.ToArray();
            var writeTask = writingTask;

            loggingTasks.Clear();
            writingTask = null;

            var waitTasks = new List<System.Threading.Tasks.Task>();

            foreach (var task in loggingSnapshot)
            {
                try { task.Stop(); } catch { }
                waitTasks.Add(task.AwaitAsync());
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
                    await System.Threading.Tasks.Task.WhenAll(waitTasks).ConfigureAwait(false);
                }
                catch
                {
                    // ignore exceptions during stopping
                }
            }
        }
        /// <summary>
        /// Checks if the database connection can be successfully opened.
        /// </summary>
        /// <returns><c>true</c> if the database is accessible; otherwise, <c>false</c>.</returns>
        public bool DatabaseIsOpen()
        {
            try
            {
                using (var database = new SQLiteConnection(connectionString))
                {
                    database.Open();
                    database.Close();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }
        /// <summary>
        /// Checks if the database connection is closed.
        /// </summary>
        /// <returns><c>true</c> if the database is not accessible; otherwise, <c>false</c>.</returns>
        public bool DatabaseIsClosed()
        {
            return !DatabaseIsOpen();
        }
        private void RunLogger(System.Threading.CancellationToken token, int interval, string logger)
        {
            string insertValues = "";

            foreach (var item in logList)
                if (item.Value.SqlTable == logger) insertValues += item.Value.Name + ",";

            if (insertValues.Length == 0)
                return;

            insertValues = insertValues.Substring(0, insertValues.Length - 1);

            double intervalTicks = interval * (Stopwatch.Frequency / 1000.0);
            long nextTick = Stopwatch.GetTimestamp();

            try
            {
                while (!token.IsCancellationRequested)
                {
                    long nowTick = Stopwatch.GetTimestamp();
                    long remainingTicks = nextTick - nowTick;

                    if (remainingTicks > 0)
                    {
                        while (remainingTicks > Stopwatch.Frequency / 1000 && !token.IsCancellationRequested)
                        {
                            System.Threading.Thread.Sleep(1);
                            nowTick = Stopwatch.GetTimestamp();
                            remainingTicks = nextTick - nowTick;
                        }

                        while (Stopwatch.GetTimestamp() < nextTick && !token.IsCancellationRequested)
                        {
                            System.Threading.Thread.SpinWait(50);
                        }
                    }

                    if (token.IsCancellationRequested)
                        break;

                    timeRel = DateTime.Now - start;
                    Lines.Enqueue($"INSERT INTO {logger} (datetime, timeRel, {insertValues}) VALUES ('{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)}','{timeRel.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture)}',{getValues(logger)})");

                    nextTick += (long)intervalTicks;

                    nowTick = Stopwatch.GetTimestamp();
                    if (nowTick > nextTick + (long)intervalTicks)
                    {
                        nextTick = nowTick + (long)intervalTicks;
                    }
                }
            }
            catch (TaskCanceledException)
            {
               Debug.WriteLine($"{Name}.SqlLogger logging task canceled successfully.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{Name}.SqlLogger logging task encountered an error: {ex.Message}");
            }

        }
        private async Task WriteLogsToFile(System.Threading.CancellationToken token)
        {
            //myWriter = new StreamWriter(Folder + "\\" + Filename, append: true, encoding: Encoding.UTF8);

            using (var database = new SQLiteConnection(connectionString))
            {
                try
                {
                    database.Open();
                    while (!token.IsCancellationRequested || !Lines.IsEmpty)
                    {
                        while (Lines.TryDequeue(out var cmd))
                        {
                            if (string.IsNullOrWhiteSpace(cmd))
                            {
                                continue;
                            }

                            SQLiteCommand command = new SQLiteCommand(cmd, database);
                            command.ExecuteNonQuery();
                        }

                        if (!token.IsCancellationRequested)
                            await Task.Delay(50, token); // Adjust delay for batch writing
                    }
                    database.Close();
                }
                catch (TaskCanceledException)
                {
                    Debug.WriteLine($"{Name}.SqlLogger writing task canceled successfully.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"{Name}.SqlLogger writing task encountered an error: {ex.Message}");
                }
                finally
                {
                    database.Close();
                }
            }

        }
        /// <summary>
        /// Retrieves and formats the current values of all data points for a specific logger table.
        /// </summary>
        /// <param name="logger">The name of the logger table (e.g., "p1000").</param>
        /// <returns>A comma-separated string of formatted values for an SQL INSERT statement.</returns>
        private static string EscapeSql(string value)
        {
            return value?.Replace("'", "''") ?? string.Empty;
        }

        public virtual string getValues(string logger)
        {

            try
            {
                var stringBuilder = new StringBuilder();

                foreach (var item in logList)
                {
                    if (item.Value.SqlTable != logger) continue;
                    var currentValue = item.Value.CurrentValue;

                    stringBuilder.Append(ToSqlLiteral(currentValue, item.Value.Format)).Append(",");
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

        private static string DetectSqlValueType(object value)
        {
            if (value == null)
                return "TEXT";

            Type valueType = Nullable.GetUnderlyingType(value.GetType()) ?? value.GetType();
            TypeCode typeCode = Type.GetTypeCode(valueType);

            return typeCode switch
            {
                TypeCode.Single or TypeCode.Double or TypeCode.Decimal => "REAL",
                TypeCode.SByte or TypeCode.Byte or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 => "REAL",
                TypeCode.DateTime => "TEXT",
                _ => "TEXT"
            };
        }

        private static string ToSqlLiteral(object? value, string format)
        {
            if (value == null)
                return "NULL";

            bool hasFormat = !string.IsNullOrWhiteSpace(format);

            return value switch
            {
                double d => $"'{(hasFormat ? d.ToString(format, CultureInfo.InvariantCulture) : d.ToString(CultureInfo.InvariantCulture))}'",
                float f => $"'{(hasFormat ? f.ToString(format, CultureInfo.InvariantCulture) : f.ToString(CultureInfo.InvariantCulture))}'",
                decimal m => $"'{(hasFormat ? m.ToString(format, CultureInfo.InvariantCulture) : m.ToString(CultureInfo.InvariantCulture))}'",
                DateTime dt => $"'{EscapeSql(dt.ToString(hasFormat ? format : "yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture))}'",
                string s => $"'{EscapeSql(s)}'",
                IFormattable formattable when hasFormat => $"'{EscapeSql(formattable.ToString(format, CultureInfo.InvariantCulture))}'",
                _ => $"'{EscapeSql(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty)}'"
            };
        }
    }
}

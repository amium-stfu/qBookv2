using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Amium.Host
{
    public sealed class RuntimeResourceScope : IDisposable
    {
        private static readonly object ScopeSync = new();
        private static readonly List<RuntimeResourceScope> _scopes = new();
        private static int _nextGeneration;
        private static RuntimeResourceScope? _current;

        private readonly object _sync = new();
        private readonly List<IManagedTask> _tasks = new();
        private readonly List<AThread> _threads = new();
        private readonly List<ATimerBase> _timers = new();
        private readonly List<INetworkConnection> _connections = new();
        private readonly List<CancellationTokenSource> _sources = new();
        private int _disposed;

        private RuntimeResourceScope(int generation, string reason)
        {
            Generation = generation;
            CreatedAtUtc = DateTime.UtcNow;
            Reason = reason;

            lock (ScopeSync)
            {
                _scopes.Add(this);
            }
        }

        public int Generation { get; }
        public DateTime CreatedAtUtc { get; }
        public string Reason { get; }

        public static RuntimeResourceScope Current
        {
            get
            {
                lock (ScopeSync)
                {
                    return _current ??= new RuntimeResourceScope(0, "bootstrap");
                }
            }
        }

        public static RuntimeResourceScope BeginNewScope(string reason)
        {
            RuntimeResourceScope? previous = null;
            RuntimeResourceScope next;

            lock (ScopeSync)
            {
                previous = _current;
                next = new RuntimeResourceScope(Interlocked.Increment(ref _nextGeneration), reason);
                _current = next;
            }

            if (previous != null)
            {
                previous.Dispose();
            }

            Core.LogInfo($"[RuntimeScope] Activated generation {next.Generation} ({reason}).");
            return next;
        }

        public static void DisposeCurrent(string reason)
        {
            RuntimeResourceScope? scope;
            lock (ScopeSync)
            {
                scope = _current;
                _current = null;
            }

            if (scope == null)
            {
                Core.LogDebug($"[RuntimeScope] No active scope to dispose ({reason}).");
                return;
            }

            Core.LogInfo($"[RuntimeScope] Disposing generation {scope.Generation} ({reason}).");
            scope.Dispose();
        }

        public static void ShutdownAll(string reason)
        {
            List<RuntimeResourceScope> scopes;

            lock (ScopeSync)
            {
                scopes = _scopes
                    .OrderByDescending(scope => scope.Generation)
                    .ToList();
                _current = null;
            }

            if (scopes.Count == 0)
            {
                Core.LogDebug($"[RuntimeScope] No active scopes to dispose ({reason}).");
                return;
            }

            Core.LogInfo($"[RuntimeScope] Disposing {scopes.Count} scope(s) ({reason}).");

            foreach (var scope in scopes)
            {
                scope.Dispose();
            }
        }

        internal void RegisterTask(IManagedTask task)
        {
            lock (_sync)
            {
                if (!_tasks.Contains(task))
                {
                    _tasks.Add(task);
                }
            }
        }

        internal void DeregisterTask(IManagedTask task)
        {
            lock (_sync)
            {
                _tasks.Remove(task);
            }
        }

        internal void RegisterThread(AThread thread)
        {
            lock (_sync)
            {
                if (!_threads.Contains(thread))
                {
                    _threads.Add(thread);
                }
            }
        }

        internal void DeregisterThread(AThread thread)
        {
            lock (_sync)
            {
                _threads.Remove(thread);
            }
        }

        internal void RegisterTimer(ATimerBase timer)
        {
            lock (_sync)
            {
                if (!_timers.Contains(timer))
                {
                    _timers.Add(timer);
                }
            }
        }

        internal void DeregisterTimer(ATimerBase timer)
        {
            lock (_sync)
            {
                _timers.Remove(timer);
            }
        }

        internal void RegisterConnection(INetworkConnection connection)
        {
            lock (_sync)
            {
                if (!_connections.Contains(connection))
                {
                    _connections.Add(connection);
                }
            }
        }

        internal void DeregisterConnection(INetworkConnection connection)
        {
            lock (_sync)
            {
                _connections.Remove(connection);
            }
        }

        internal void RegisterTokenSource(CancellationTokenSource source)
        {
            lock (_sync)
            {
                if (!_sources.Contains(source))
                {
                    _sources.Add(source);
                }
            }
        }

        internal void DeregisterTokenSource(CancellationTokenSource source)
        {
            lock (_sync)
            {
                _sources.Remove(source);
            }
        }

        internal IReadOnlyList<CancellationTokenSource> TokenSourcesSnapshot()
        {
            lock (_sync)
            {
                return _sources.ToList().AsReadOnly();
            }
        }

        internal IReadOnlyList<INetworkConnection> ConnectionsSnapshot()
        {
            lock (_sync)
            {
                return _connections.ToList().AsReadOnly();
            }
        }

        internal void StopAllTasks()
        {
            foreach (var task in Snapshot(_tasks))
            {
                try
                {
                    task.Stop();
                }
                catch (Exception ex)
                {
                    Core.LogDebug($"[RuntimeScope] Task stop failed for {task.InstanceName}: {ex.Message}");
                }
            }
        }

        internal void StopAllThreads()
        {
            foreach (var thread in Snapshot(_threads))
            {
                try
                {
                    thread.Stop();
                }
                catch (Exception ex)
                {
                    Core.LogDebug($"[RuntimeScope] Thread stop failed for {thread.InstanceName}: {ex.Message}");
                }
            }
        }

        internal void StopAllTimers()
        {
            foreach (var timer in Snapshot(_timers))
            {
                try
                {
                    timer.Stop();
                }
                catch (Exception ex)
                {
                    Core.LogDebug($"[RuntimeScope] Timer stop failed for {timer.InstanceName}: {ex.Message}");
                }
            }
        }

        internal void DisposeAllTimers()
        {
            foreach (var timer in Snapshot(_timers))
            {
                try
                {
                    timer.Dispose();
                }
                catch (Exception ex)
                {
                    Core.LogDebug($"[RuntimeScope] Timer dispose failed for {timer.InstanceName}: {ex.Message}");
                }
            }
        }

        internal void CloseAllConnections()
        {
            foreach (var connection in Snapshot(_connections))
            {
                try
                {
                    connection.Close();
                }
                catch (Exception ex)
                {
                    Core.LogDebug($"[RuntimeScope] Connection close failed for {connection.InstanceName}: {ex.Message}");
                }
            }
        }

        internal void CancelAllTokens()
        {
            foreach (var source in Snapshot(_sources))
            {
                try
                {
                    source.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }
                finally
                {
                    try
                    {
                        source.Dispose();
                    }
                    catch (ObjectDisposedException)
                    {
                    }

                    TokenManager.Deregister(source);
                }
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            lock (ScopeSync)
            {
                _scopes.Remove(this);
                if (ReferenceEquals(_current, this))
                {
                    _current = null;
                }
            }

            StopAllTasks();
            StopAllThreads();
            DisposeAllTimers();
            CancelAllTokens();
            CloseAllConnections();

            ReportLeaks();
        }

        private void ReportLeaks()
        {
            var leakingTasks = Snapshot(_tasks).Select(t => t.InstanceName).ToList();
            var leakingThreads = Snapshot(_threads).Where(t => t.IsRunning).Select(t => t.InstanceName).ToList();
            var leakingTimers = Snapshot(_timers).Where(t => t.IsRunning).Select(t => t.InstanceName).ToList();
            var leakingConnections = Snapshot(_connections).Where(c => c.IsOpen).Select(c => c.InstanceName).ToList();
            var leakingTokens = Snapshot(_sources).Select(_ => "<token-source>").ToList();

            if (leakingTasks.Count == 0 &&
                leakingThreads.Count == 0 &&
                leakingTimers.Count == 0 &&
                leakingConnections.Count == 0 &&
                leakingTokens.Count == 0)
            {
                Core.LogInfo($"[RuntimeScope] Generation {Generation} disposed cleanly.");
                return;
            }

            Core.LogWarn(
                $"[RuntimeScope] Generation {Generation} left resources behind. " +
                $"Tasks={Format(leakingTasks)} Threads={Format(leakingThreads)} Timers={Format(leakingTimers)} " +
                $"Sockets={Format(leakingConnections)} Tokens={Format(leakingTokens)}");
        }

        private List<T> Snapshot<T>(List<T> source)
        {
            lock (_sync)
            {
                return source.ToList();
            }
        }

        private static string Format(List<string> names)
        {
            return names.Count == 0 ? "0" : string.Join(", ", names);
        }
    }
}

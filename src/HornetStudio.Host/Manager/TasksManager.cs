using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HornetStudio.Host
{
    internal interface IManagedTask
    {
        string InstanceName { get; }
        void Stop();
    }

    public class ATask : IManagedTask, IDisposable
    {
        public string InstanceName { get; }
        private readonly CancellationTokenSource _cts = new();
        private Task? _task;
        private int _cleanupState;
        public bool IsRunning => _task != null && !_task.IsCompleted && !_cts.IsCancellationRequested;

        public event Action? OnCompleted;
        public event Action<Exception>? OnException;
        public event Action? OnCancelled;

        public ATask(string instanceName, Func<CancellationToken, Task> work)
        {
            InstanceName = instanceName;
            TasksManager.Register(this);

            _task = Task.Run(async () =>
            {
                try
                {
                    await work(_cts.Token);
                    OnCompleted?.Invoke();
                }
                catch (OperationCanceledException)
                {
                    Core.LogDebug($"[ATask] {InstanceName} cancelled.");
                    OnCancelled?.Invoke();
                    throw;
                }
                catch (Exception ex)
                {
                    Core.LogDebug($"[ATask] {InstanceName} exception: {ex.Message}");
                    OnException?.Invoke(ex);
                    throw;
                }
                finally
                {
                    Cleanup();
                }
            }, _cts.Token);
            Core.LogDebug($"[ATask] Registered: {InstanceName}");
        }

        public async Task AwaitAsync()
        {
            if (_task == null) return;
            try { await _task; }
            catch { }
        }

        public void Stop()
        {
            Core.LogDebug($"[ATask] Stop requested: {InstanceName}");

            try
            {
                if (!_cts.IsCancellationRequested)
                    _cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            if (_task != null && !_task.IsCompleted)
            {
                try
                {
                    _task.Wait(2000);
                }
                catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is TaskCanceledException || e is OperationCanceledException))
                {
                }
                catch (Exception ex)
                {
                    Core.LogDebug($"[ATask] {InstanceName} error during stop: {ex.Message}");
                }
            }

            if (_task == null || _task.IsCompleted)
            {
                Core.LogDebug($"[ATask] Cleanly stopped: {InstanceName}");
                Cleanup();
            }
            else
            {
                Core.LogWarn($"[ATask] {InstanceName} did not finish within the stop timeout.");
            }
        }

        public void Dispose() => Cleanup();

        private void Cleanup()
        {
            if (Interlocked.Exchange(ref _cleanupState, 1) != 0)
                return;

            TasksManager.Deregister(this);
            _cts.Dispose();
        }
    }

    public class ATask<T> : IManagedTask, IDisposable
    {
        public string InstanceName { get; }
        private readonly CancellationTokenSource _cts = new();
        private Task<T>? _task;
        private int _cleanupState;
        public bool IsRunning => _task != null && !_task.IsCompleted && !_cts.IsCancellationRequested;

        public event Action<T>? OnResult;
        public event Action<Exception>? OnException;
        public event Action? OnCancelled;

        public ATask(string instanceName, Func<CancellationToken, Task<T>> work)
        {
            InstanceName = instanceName;
            TasksManager.Register(this);

            _task = Task.Run(async () =>
            {
                try
                {
                    var result = await work(_cts.Token);
                    OnResult?.Invoke(result);
                    return result;
                }
                catch (OperationCanceledException)
                {
                    Core.LogDebug($"[ATask] {InstanceName} cancelled.");
                    OnCancelled?.Invoke();
                    throw;
                }
                catch (Exception ex)
                {
                    Core.LogDebug($"[ATask] {InstanceName} exception: {ex.Message}");
                    OnException?.Invoke(ex);
                    throw;
                }
                finally
                {
                    Cleanup();
                }
            }, _cts.Token);
            Core.LogDebug($"[ATask] Registered: {InstanceName}");
        }

        public async Task<T?> AwaitAsync()
        {
            if (_task == null) return default;
            try
            {
                return await _task;
            }
            catch
            {
                return default;
            }
        }

        public void Stop()
        {
            Core.LogDebug($"[ATask] Stop requested: {InstanceName}");

            try
            {
                if (!_cts.IsCancellationRequested)
                    _cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            if (_task != null && !_task.IsCompleted)
            {
                try
                {
                    _task.Wait(2000);
                }
                catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is TaskCanceledException || e is OperationCanceledException))
                {
                    Core.LogDebug($"[ATask] {InstanceName} cancelled.");
                }
                catch (Exception ex)
                {
                    Core.LogDebug($"[ATask] {InstanceName} error during stop: {ex.Message}");
                }
            }

            if (_task == null || _task.IsCompleted)
            {
                Core.LogDebug($"[ATask] Cleanly stopped: {InstanceName}");
                Cleanup();
            }
            else
            {
                Core.LogWarn($"[ATask] {InstanceName} did not finish within the stop timeout.");
            }
        }

        public void Dispose() => Cleanup();

        private void Cleanup()
        {
            if (Interlocked.Exchange(ref _cleanupState, 1) != 0)
                return;

            TasksManager.Deregister(this);
            _cts.Dispose();
        }
    }

    public static class TasksManager
    {
        private static readonly object Sync = new();
        private static readonly Dictionary<object, RuntimeResourceScope> Owners = new();

        public static void Register<T>(ATask<T> task)
        {
            var scope = RuntimeResourceScope.Current;
            lock (Sync)
            {
                Owners[task] = scope;
            }

            scope.RegisterTask(task);
        }

        public static void Register(ATask task)
        {
            var scope = RuntimeResourceScope.Current;
            lock (Sync)
            {
                Owners[task] = scope;
            }

            scope.RegisterTask(task);
        }

        public static void Deregister<T>(ATask<T> task)
        {
            RuntimeResourceScope? scope;
            lock (Sync)
            {
                Owners.TryGetValue(task, out scope);
                Owners.Remove(task);
            }

            scope?.DeregisterTask(task);
        }

        public static void Deregister(ATask task)
        {
            RuntimeResourceScope? scope;
            lock (Sync)
            {
                Owners.TryGetValue(task, out scope);
                Owners.Remove(task);
            }

            scope?.DeregisterTask(task);
        }

        public static void StopAll()
        {
            RuntimeResourceScope.Current.StopAllTasks();
        }
    }
}

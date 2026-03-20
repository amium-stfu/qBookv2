using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace UiEditor.Host
{
    public class AThread : IDisposable
    {
        public string InstanceName { get; init; }
        private readonly Thread _thread;
        private readonly CancellationTokenSource _cts = new();
        private int _cleanupState;
        public bool IsRunning => _thread.IsAlive && !_cts.IsCancellationRequested;

        bool done = false;

        public bool IsDone
        {
            get => done;
            set
            {
                if (value && !done)
                {
                    done = true;
                    Core.LogDebug($"[AThread] {InstanceName} marked as done.");
                }
            }
        }

        public bool IsStoppRequest => _cts.IsCancellationRequested;

        public AThread(string instanceName, Action<CancellationToken> work, bool isBackground = true)
        {
            InstanceName = instanceName;

            _thread = new Thread(() =>
            {
                try
                {
                    done = false;
                    Core.LogDebug($"[AThread] {InstanceName} executing on thread {Environment.CurrentManagedThreadId}.");
                    work(_cts.Token);
                    done = true;
                    Core.LogDebug($"[AThread] {InstanceName} work completed.");
                }
                catch (OperationCanceledException)
                {
                    Core.LogDebug($"[AThread] {InstanceName} cancelled.");
                    done = true;
                }
                catch (Exception ex)
                {
                    Core.LogError($"[AThread] {InstanceName} failed.", ex);
                    done = true;
                }
                finally
                {
                    Core.LogDebug($"[AThread] {InstanceName} entering cleanup. Done={done} CancelRequested={_cts.IsCancellationRequested}");
                    Cleanup();
                }
            });
            _thread.IsBackground = isBackground;

            ThreadsManager.Register(this);
            Core.LogDebug($"[AThread] {InstanceName}: Registered");
        }

        public AThread(string instanceName, Action work, bool isBackground = true)
            : this(instanceName, _ => work(), isBackground)
        {
        }

        public void Start()
        {
            Core.LogDebug($"[AThread] {InstanceName}: Try to start");
            if ((_thread.ThreadState & ThreadState.Unstarted) == ThreadState.Unstarted)
            {
                _thread.Start();
                Core.LogDebug($"[AThread] {InstanceName}: Started");
            }
            else
            {
                Core.LogDebug($"[AThread] {InstanceName}: ThreadState {_thread.ThreadState}");
            }
        }

        public void Wait(int milliSeconds)
        {
            DateTime start = DateTime.Now;
            while (DateTime.Now < start.AddMilliseconds(milliSeconds))
            {
                if (IsStoppRequest || !IsRunning) break;
                Thread.Sleep(5);
            }
        }

        public void Stop()
        {
            Core.LogDebug($"[AThread] Stop requested: {InstanceName}");

            try
            {
                if (!_cts.IsCancellationRequested)
                    _cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            if (_thread.IsAlive && !_thread.Join(5000))
            {
                Core.LogDebug($"[AThread] Still running after Cancel: {InstanceName} - trying Interrupt...");

                try
                {
                    _thread.Interrupt();
                }
                catch (Exception ex)
                {
                    Core.LogDebug($"[AThread] Interrupt failed for {InstanceName}: {ex.Message}");
                }

                if (_thread.IsAlive && !_thread.Join(1000))
                {
                    Core.LogDebug($"[AThread] Cannot stop thread {InstanceName} cleanly.");
                }
            }

            if (!_thread.IsAlive)
            {
                Core.LogDebug($"[AThread] Cleanly stopped: {InstanceName}");
                done = true;
                Cleanup();
            }
            else
            {
                Core.LogWarn($"[AThread] {InstanceName} is still alive after stop attempt.");
            }
        }

        public void Dispose() => Cleanup();

        private void Cleanup()
        {
            if (Interlocked.Exchange(ref _cleanupState, 1) != 0)
                return;

            ThreadsManager.Deregister(this);
            _cts.Dispose();
        }
    }

    public static class ThreadsManager
    {
        private static readonly object Sync = new();
        private static readonly Dictionary<AThread, RuntimeResourceScope> Owners = new();

        public static void Register(AThread thread)
        {
            var scope = RuntimeResourceScope.Current;
            lock (Sync)
            {
                Owners[thread] = scope;
            }

            scope.RegisterThread(thread);
        }

        public static void Deregister(AThread thread)
        {
            RuntimeResourceScope? scope;
            lock (Sync)
            {
                Owners.TryGetValue(thread, out scope);
                Owners.Remove(thread);
            }

            scope?.DeregisterThread(thread);
        }

        public static void StopAll()
        {
            RuntimeResourceScope.Current.StopAllThreads();
        }
    }
}

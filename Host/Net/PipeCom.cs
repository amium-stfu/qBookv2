using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Amium.Host
{
    public static class PipeNames
    {
        private const string server = @"amium.pipe.server";
        private const string client = @"amium.pipe.client";

        static string Id = Guid.NewGuid().ToString("N");

        public static string Server = $"{server}.{Id}";
        public static string Client = $"{client}.{Id}";

        public static void ResetPipes()
        {
            Id = Guid.NewGuid().ToString("N");

            Server = $"{server}.{Id}";
            Client = $"{client}.{Id}";
        }

        public static void SavePipesToFile(string path)
        {
            var data = new
            {
                ServerPipe = Server,
                ClientPipe = Client
            };
            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
    }

    public class PipeCommand
    {
        public string? Command { get; set; }
        public string[]? Args { get; set; }
    }

    public sealed class ServerSide : IAsyncDisposable
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _receiveTask;
        private readonly BlockingCollection<PipeCommand> _sendQueue = new();
        private readonly Task _sendTask;
        private readonly Task _eventAcceptTask;
        private NamedPipeServerStream? _eventServer;
        private StreamWriter? _eventWriter;
        private readonly SemaphoreSlim _sendLock = new(1, 1);

        public event Action<PipeCommand>? OnReceived;

        public ServerSide()
        {
            _receiveTask = Task.Run(() => CommandLoopAsync(_cts.Token));
            _sendTask = Task.Run(() => SendLoopAsync(_cts.Token));
            _eventAcceptTask = Task.Run(() => EventAcceptLoopAsync(_cts.Token));
        }

        public void Send(PipeCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (!IsEventConnected())
            {
                return;
            }

            _sendQueue.Add(command, _cts.Token);
        }

        public Task SendAsync(PipeCommand command, CancellationToken cancellationToken = default)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (!IsEventConnected())
            {
                return Task.CompletedTask;
            }

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
            _sendQueue.Add(command, linkedCts.Token);
            return Task.CompletedTask;
        }

        private async Task SendLoopAsync(CancellationToken token)
        {
            try
            {
                foreach (var command in _sendQueue.GetConsumingEnumerable(token))
                {
                    try
                    {
                        await SendInternalAsync(command, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (IOException ex)
                    {
                        Core.LogDebug($"Pipe send failed for command {command.Command}. Event connection will be recreated.", ex);
                        CleanupEventConnection();
                    }
                    catch (Exception ex)
                    {
                        Core.LogWarn($"Unexpected pipe send failure for command {command.Command}", ex);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task SendInternalAsync(PipeCommand command, CancellationToken token)
        {
            await _sendLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (!IsEventConnected())
                {
                    return;
                }

                var writer = _eventWriter;
                if (writer is null)
                {
                    return;
                }

                string line = JsonSerializer.Serialize(command);
                await writer.WriteLineAsync(line).ConfigureAwait(false);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private bool IsEventConnected()
            => _eventServer is not null && _eventServer.IsConnected && _eventWriter is not null;

        private void CleanupEventConnection()
        {
            try { _eventWriter?.Dispose(); } catch { }
            try { _eventServer?.Dispose(); } catch { }
            _eventWriter = null;
            _eventServer = null;
        }

        private async Task EventAcceptLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    CleanupEventConnection();

                    var server = new NamedPipeServerStream(PipeNames.Client, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    _eventServer = server;

                    await server.WaitForConnectionAsync(token).ConfigureAwait(false);

                    _eventWriter = new StreamWriter(server, Encoding.UTF8)
                    {
                        AutoFlush = true
                    };

                    while (!token.IsCancellationRequested && server.IsConnected)
                    {
                        await Task.Delay(200, token).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (IOException ex)
                {
                    Core.LogDebug("Pipe event accept loop connection dropped. Waiting for reconnect.", ex);
                    await Task.Delay(200, token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Core.LogWarn("Unexpected error in pipe event accept loop", ex);
                    await Task.Delay(200, token).ConfigureAwait(false);
                }
                finally
                {
                    CleanupEventConnection();
                }
            }
        }

        private async Task CommandLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(PipeNames.Server, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    await server.WaitForConnectionAsync(token).ConfigureAwait(false);

                    using var reader = new StreamReader(server, Encoding.UTF8);
                    string? line;

                    while (!token.IsCancellationRequested &&
                           (line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }

                        try
                        {
                            var cmd = JsonSerializer.Deserialize<PipeCommand>(line);
                            if (cmd != null)
                            {
                                OnReceived?.Invoke(cmd);
                            }
                        }
                        catch (JsonException ex)
                        {
                            Core.LogWarn("Invalid pipe command payload received", ex);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (IOException ex)
                {
                    Core.LogDebug("Pipe command loop connection dropped. Waiting for reconnect.", ex);
                    await Task.Delay(200, token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Core.LogWarn("Unexpected error in pipe command loop", ex);
                    await Task.Delay(200, token).ConfigureAwait(false);
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            _sendQueue.CompleteAdding();

            try { await _receiveTask.ConfigureAwait(false); } catch { }
            try { await _sendTask.ConfigureAwait(false); } catch { }
            try { await _eventAcceptTask.ConfigureAwait(false); } catch { }

            _cts.Dispose();
            _sendLock.Dispose();
            _sendQueue.Dispose();
            CleanupEventConnection();
        }
    }

    public sealed class ClientSide : IAsyncDisposable
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _receiveTask;
        private readonly BlockingCollection<PipeCommand> _sendQueue = new();
        private readonly Task _sendTask;

        public event Action<PipeCommand>? OnReceived;

        public ClientSide()
        {
            _receiveTask = Task.Run(() => EventLoopAsync(_cts.Token));
            _sendTask = Task.Run(() => SendLoopAsync(_cts.Token));
        }

        public void Send(PipeCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            _sendQueue.Add(command, _cts.Token);
        }

        public Task SendAsync(PipeCommand command, CancellationToken cancellationToken = default)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
            _sendQueue.Add(command, linkedCts.Token);
            return Task.CompletedTask;
        }

        private async Task SendLoopAsync(CancellationToken token)
        {
            try
            {
                foreach (var command in _sendQueue.GetConsumingEnumerable(token))
                {
                    try
                    {
                        await SendInternalAsync(command, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (IOException ex)
                    {
                        Core.LogDebug($"Pipe client send failed for command {command.Command}", ex);
                    }
                    catch (Exception ex)
                    {
                        Core.LogWarn($"Unexpected client send failure for command {command.Command}", ex);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private static async Task SendInternalAsync(PipeCommand command, CancellationToken token)
        {
            using var client = new NamedPipeClientStream(".", PipeNames.Server, PipeDirection.Out, PipeOptions.Asynchronous);
            try
            {
                client.Connect(50);
            }
            catch (TimeoutException)
            {
                return;
            }

            if (!client.IsConnected)
            {
                return;
            }

            using var writer = new StreamWriter(client, Encoding.UTF8)
            {
                AutoFlush = true
            };

            string line = JsonSerializer.Serialize(command);
            await writer.WriteLineAsync(line).ConfigureAwait(false);
        }

        private async Task EventLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var client = new NamedPipeClientStream(".", PipeNames.Client, PipeDirection.In, PipeOptions.Asynchronous);
                    await client.ConnectAsync(token).ConfigureAwait(false);

                    using var reader = new StreamReader(client, Encoding.UTF8);
                    string? line;

                    while (!token.IsCancellationRequested &&
                           (line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }

                        try
                        {
                            var cmd = JsonSerializer.Deserialize<PipeCommand>(line);
                            if (cmd != null)
                            {
                                OnReceived?.Invoke(cmd);
                            }
                        }
                        catch (JsonException ex)
                        {
                            Core.LogWarn("Invalid pipe event payload received", ex);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (IOException ex)
                {
                    Core.LogDebug("Pipe event loop connection dropped. Waiting for reconnect.", ex);
                    await Task.Delay(200, token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Core.LogWarn("Unexpected error in pipe event loop", ex);
                    await Task.Delay(200, token).ConfigureAwait(false);
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            _sendQueue.CompleteAdding();

            try { await _receiveTask.ConfigureAwait(false); } catch { }
            try { await _sendTask.ConfigureAwait(false); } catch { }

            _cts.Dispose();
            _sendQueue.Dispose();
        }
    }
}

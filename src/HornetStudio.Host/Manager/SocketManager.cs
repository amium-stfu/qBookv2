using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;


namespace HornetStudio.Host
{
    public interface INetworkConnection : IDisposable
    {
        string InstanceName { get; }
        bool IsOpen { get; }
        void Close();
    }

    // Erweiterter TCP-Client-Wrapper mit Receive-Loop und Event
    public class ATcpConnection : INetworkConnection
    {
        public string InstanceName { get; }
        private readonly TcpClient _client;
        private NetworkStream? _stream;
        private readonly CancellationTokenSource _cts;
        public bool IsOpen => _client.Connected;

        // Event für eingehende Daten
        public event Action<byte[]>? DataReceived;

        public ATcpConnection(string name, TcpClient client)
        {
            InstanceName = name;
            _client = client ?? throw new ArgumentNullException(nameof(client));
            if (_client.Connected)
                _stream = _client.GetStream();
            _cts = new CancellationTokenSource();
            SocketManager.Register(this);
        }

        // Starte asynchrones Lesen
        public void StartReceiving()
        {
            if (_stream == null)
                _stream = _client.GetStream();

            Task.Run(() => ReceiveLoop(_cts.Token), _cts.Token);
        }

        private async Task ReceiveLoop(CancellationToken token)
        {
            var buffer = new byte[4096];
            var stream = _stream ?? _client.GetStream();
            _stream = stream;
            try
            {
                while (!token.IsCancellationRequested && _client.Connected)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                    if (bytesRead == 0) // Verbindung wurde geschlossen
                        break;
                    var data = new byte[bytesRead];
                    Array.Copy(buffer, data, bytesRead);
                    DataReceived?.Invoke(data);
                }
            }
            catch (OperationCanceledException) { /* Expected */ }
            catch (Exception ex)
            {
                Console.WriteLine($"[ATcpConnection] ReceiveLoop Fehler: {ex.Message}");
            }
        }

        // Daten senden (async)
        public async Task SendAsync(byte[] data)
        {
            if (!IsOpen)
                throw new InvalidOperationException("Connection is not open");
            if (_stream == null)
                _stream = _client.GetStream();

            await _stream.WriteAsync(data, 0, data.Length);
        }

        public void Close()
        {
            try
            {
                _cts?.Cancel();
                _stream?.Close();
                _client?.Close();
            }
            catch { }
            finally
            {
                SocketManager.Deregister(this);
            }
        }

        public void Dispose()
        {
            Close();
        }
    }

    // Der zentrale Manager
    public static class SocketManager
    {
        private static readonly object Sync = new();
        private static readonly Dictionary<INetworkConnection, RuntimeResourceScope> Owners = new();

        public static void Register(INetworkConnection conn)
        {
            var scope = RuntimeResourceScope.Current;
            lock (Sync)
            {
                Owners[conn] = scope;
            }

            scope.RegisterConnection(conn);
        }

        public static void Deregister(INetworkConnection conn)
        {
            RuntimeResourceScope? scope;
            lock (Sync)
            {
                Owners.TryGetValue(conn, out scope);
                Owners.Remove(conn);
            }

            scope?.DeregisterConnection(conn);
        }

        public static void CloseAll()
        {
            RuntimeResourceScope.Current.CloseAllConnections();
        }

        // Für Status, Debugging, Monitoring etc.
        public static IEnumerable<INetworkConnection> All => RuntimeResourceScope.Current.ConnectionsSnapshot();
    }
}

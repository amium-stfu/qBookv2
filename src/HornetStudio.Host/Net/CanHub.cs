using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using HornetStudio.Logging;

namespace HornetStudio.Host
{
    /// <summary>
    /// Central CAN-over-UDP hub for the host process.
    ///
    /// Listens on a single UDP port and dispatches received CAN frames
    /// to registered logical clients based on the sender endpoint.
    /// </summary>
    public sealed class CanHub : IAsyncDisposable
    {
        public delegate void FrameReceivedHandler(EndPoint remoteEndpoint, uint id, byte dlc, byte[] data);
        public delegate void DiagnosticHandler(string message);

        private readonly UdpClient _udpClient;
        private int _txPackageCounter;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _rxTask;

        public event FrameReceivedHandler? FrameReceived;
        public event DiagnosticHandler? Diagnostic;

        public int LocalPort => ((IPEndPoint?)_udpClient.Client.LocalEndPoint)?.Port ?? 0;

        public CanHub(int port)
        {
            if (port <= 0 || port > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(port));
            }

            _udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, port));
            WriteDiagnostic($"[CanHub] udp socket bound local={_udpClient.Client.LocalEndPoint}");

            _rxTask = Task.Run(() => RxLoopAsync(_cts.Token), _cts.Token);
        }

        /// <summary>
        /// Transmits a single CAN frame to the specified remote endpoint using
        /// the same UDP framing as the original UdlClient.Can implementation.
        /// </summary>
        public void Transmit(IPEndPoint remoteEndpoint, uint id, byte dlc, byte[] data)
        {
            if (remoteEndpoint is null)
            {
                throw new ArgumentNullException(nameof(remoteEndpoint));
            }

            if (data is null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (dlc > 8)
            {
                dlc = 8;
            }

            var payloadLength = Math.Min(data.Length, dlc);
            var recordLength = 12 + payloadLength;
            var buffer = new byte[4 + recordLength];

            // Paket-Counter wie im alten Can.AppendMessage benutzen.
            var packageCounter = Interlocked.Increment(ref _txPackageCounter) - 1;
            buffer[0] = (byte)(packageCounter >> 24);
            buffer[1] = (byte)(packageCounter >> 16);
            buffer[2] = (byte)(packageCounter >> 8);
            buffer[3] = (byte)packageCounter;

            var offset = 4;
            var timestamp = DateTime.UtcNow.Ticks;

            buffer[offset] = (byte)recordLength;
            buffer[offset + 1] = (byte)(timestamp >> 24);
            buffer[offset + 2] = (byte)(timestamp >> 16);
            buffer[offset + 3] = (byte)(timestamp >> 8);
            buffer[offset + 4] = (byte)timestamp;
            buffer[offset + 5] = 0;
            buffer[offset + 6] = 0;
            buffer[offset + 7] = (byte)(id >> 24);
            buffer[offset + 8] = (byte)(id >> 16);
            buffer[offset + 9] = (byte)(id >> 8);
            buffer[offset + 10] = (byte)id;
            buffer[offset + 11] = (byte)payloadLength;

            if (payloadLength > 0)
            {
                Array.Copy(data, 0, buffer, offset + 12, payloadLength);
            }

            try
            {
                _udpClient.Send(buffer, buffer.Length, remoteEndpoint);
            }
            catch (Exception exception)
            {
                HostLogger.Log.Error(exception, "[CanHub] tx packet failed remote={Remote} id=0x{Id:X3} dlc={Dlc}", remoteEndpoint, id, dlc);
            }
        }

        private async Task RxLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    UdpReceiveResult result;
                    try
                    {
                        result = await _udpClient.ReceiveAsync(token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    var bytes = result.Buffer;
                    var remote = result.RemoteEndPoint;
                    if (bytes.Length < 4)
                    {
                        WriteDiagnostic($"[CanHub] rx packet too short bytes={bytes.Length} from={remote}");
                        continue;
                    }

                    // Frames sind im gleichen Format kodiert wie im UdlClient-Can:
                    // 4 Bytes Paket-Counter, danach wiederholt 12-Byte-Header + Nutzdaten.
                    var offset = 4;
                    var frameCount = 0;
                    while (bytes.Length >= offset + 12)
                    {
                        var id = (uint)((bytes[offset + 7] << 24)
                                        | (bytes[offset + 8] << 16)
                                        | (bytes[offset + 9] << 8)
                                        | bytes[offset + 10]);

                        var dlc = bytes[offset + 11];
                        if (dlc > 8)
                        {
                            dlc = 8;
                        }

                        if (bytes.Length < offset + 12 + dlc)
                        {
                            break;
                        }

                        var data = new byte[dlc];
                        Array.Copy(bytes, offset + 12, data, 0, dlc);
                        frameCount++;

                        FrameReceived?.Invoke(remote, id, dlc, data);

                        offset += 12 + dlc;
                    }

                    if (frameCount == 0)
                    {
                        WriteDiagnostic($"[CanHub] rx packet had no decodable frames bytes={bytes.Length} from={remote}");
                    }
                }
            }
            catch (Exception exception) when (!token.IsCancellationRequested)
            {
                WriteDiagnostic($"[CanHub] rx loop error={exception.GetType().Name}: {exception.Message}");
            }

            WriteDiagnostic("[CanHub] rx loop exited");
        }

        public ValueTask DisposeAsync()
        {
            _cts.Cancel();
            _udpClient.Close();
            _udpClient.Dispose();
            _cts.Dispose();
            return ValueTask.CompletedTask;
        }

        private void WriteDiagnostic(string message)
        {
            try
            {
                // Immer in den Host-Log schreiben, unabhängig vom UI-Status.
                HostLogger.Log.Debug("{Message}", message);
            }
            catch
            {
                // Loggingfehler ignorieren.
            }

            try
            {
                Diagnostic?.Invoke(message);
            }
            catch
            {
                // Best-effort only.
            }
        }
    }
}

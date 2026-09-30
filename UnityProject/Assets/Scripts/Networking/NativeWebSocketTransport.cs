#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RpsArena.Networking
{
    public sealed class NativeWebSocketTransport : IRealtimeTransport
    {
        private readonly ConcurrentQueue<Action> _mainThread = new();
        private ClientWebSocket _socket;
        private CancellationTokenSource _cts;

        public event Action Connected;
        public event Action<string> MessageReceived;
        public event Action<string> Closed;
        public bool IsConnected => _socket?.State == WebSocketState.Open;

        public async Task ConnectAsync(string url)
        {
            await DisconnectAsync();
            _cts = new CancellationTokenSource();
            _socket = new ClientWebSocket();
            await _socket.ConnectAsync(new Uri(url), _cts.Token);
            _mainThread.Enqueue(() => Connected?.Invoke());
            _ = ReceiveLoopAsync();
        }

        public async Task SendAsync(string message)
        {
            if (!IsConnected) return;
            var bytes = Encoding.UTF8.GetBytes(message);
            await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts.Token);
        }

        public async Task DisconnectAsync()
        {
            try
            {
                _cts?.Cancel();
                if (_socket?.State == WebSocketState.Open)
                    await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "client", CancellationToken.None);
            }
            catch { }
            finally
            {
                _socket?.Dispose();
                _socket = null;
                _cts?.Dispose();
                _cts = null;
            }
        }

        public void Tick()
        {
            while (_mainThread.TryDequeue(out var action)) action();
        }

        private async Task ReceiveLoopAsync()
        {
            var buffer = new byte[8192];
            try
            {
                while (_socket?.State == WebSocketState.Open && _cts is not null)
                {
                    using var ms = new System.IO.MemoryStream();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                        if (result.MessageType == WebSocketMessageType.Close) break;
                        ms.Write(buffer, 0, result.Count);
                    } while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Close) break;
                    var json = Encoding.UTF8.GetString(ms.ToArray());
                    _mainThread.Enqueue(() => MessageReceived?.Invoke(json));
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _mainThread.Enqueue(() => Closed?.Invoke(ex.Message));
            }
        }
    }
}
#endif

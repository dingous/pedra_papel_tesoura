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
        private readonly ConcurrentQueue<Action> _mainThread=new();
        private readonly SemaphoreSlim _send=new(1,1);
        private ClientWebSocket _socket;
        private CancellationTokenSource _cts;
        public event Action Connected;
        public event Action<string> MessageReceived;
        public event Action<string> Closed;
        public bool IsConnected=>_socket?.State==WebSocketState.Open;
        public async Task ConnectAsync(string url)
        {
            await DisconnectAsync();
            var cts=_cts=new CancellationTokenSource(); var socket=_socket=new ClientWebSocket();
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cts.Token); timeout.CancelAfter(15000);
            await socket.ConnectAsync(new Uri(url),timeout.Token);
            _mainThread.Enqueue(()=>{if(socket==_socket) Connected?.Invoke();});
            _ = Receive(socket,cts.Token);
        }
        public async Task SendAsync(string message)
        {
            if(!IsConnected) throw new InvalidOperationException("WebSocket desconectado.");
            var socket=_socket; var token=_cts.Token; await _send.WaitAsync(token);
            try { var bytes=Encoding.UTF8.GetBytes(message); await socket.SendAsync(new ArraySegment<byte>(bytes),WebSocketMessageType.Text,true,token); }
            finally { _send.Release(); }
        }
        public Task DisconnectAsync()
        {
            var socket=_socket; _socket=null; var cts=_cts; _cts=null;
            cts?.Cancel(); socket?.Abort(); socket?.Dispose(); cts?.Dispose(); return Task.CompletedTask;
        }
        public void Tick() { while(_mainThread.TryDequeue(out var action)) action(); }
        private async Task Receive(ClientWebSocket socket,CancellationToken token)
        {
            string reason="Conexão encerrada."; var buffer=new byte[8192];
            try
            {
                while(socket.State==WebSocketState.Open && !token.IsCancellationRequested)
                {
                    using var stream=new System.IO.MemoryStream(); WebSocketReceiveResult result;
                    do
                    {
                        result=await socket.ReceiveAsync(new ArraySegment<byte>(buffer),token);
                        if(result.MessageType==WebSocketMessageType.Close) break;
                        stream.Write(buffer,0,result.Count);
                        if(stream.Length>1024*1024) throw new InvalidOperationException("Mensagem excessiva.");
                    } while(!result.EndOfMessage);
                    if(result.MessageType==WebSocketMessageType.Close) break;
                    var json=Encoding.UTF8.GetString(stream.ToArray());
                    _mainThread.Enqueue(()=>{if(socket==_socket) MessageReceived?.Invoke(json);});
                }
            }
            catch(OperationCanceledException) { }
            catch(Exception) { reason="WebSocket indisponível."; }
            finally { _mainThread.Enqueue(()=>{if(socket==_socket) Closed?.Invoke(reason);}); }
        }
    }
}
#endif

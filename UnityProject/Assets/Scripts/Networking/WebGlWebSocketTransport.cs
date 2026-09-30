#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace RpsArena.Networking
{
    public sealed class WebGlWebSocketTransport : IRealtimeTransport
    {
        [DllImport("__Internal")] private static extern void RpsWsConnect(string url);
        [DllImport("__Internal")] private static extern void RpsWsSend(string message);
        [DllImport("__Internal")] private static extern void RpsWsClose();

        public event Action Connected;
        public event Action<string> MessageReceived;
        public event Action<string> Closed;
        public bool IsConnected { get; private set; }

        public Task ConnectAsync(string url) { RpsWsConnect(url); return Task.CompletedTask; }
        public Task SendAsync(string message) { if (IsConnected) RpsWsSend(message); return Task.CompletedTask; }
        public Task DisconnectAsync() { RpsWsClose(); IsConnected = false; return Task.CompletedTask; }
        public void Tick() { }

        public void OnOpen() { IsConnected = true; Connected?.Invoke(); }
        public void OnMessage(string message) => MessageReceived?.Invoke(message);
        public void OnClose(string reason) { IsConnected = false; Closed?.Invoke(reason); }
    }
}
#endif

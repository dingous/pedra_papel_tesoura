using System;
using System.Threading.Tasks;

namespace RpsArena.Networking
{
    public interface IRealtimeTransport
    {
        event Action Connected;
        event Action<string> MessageReceived;
        event Action<string> Closed;
        bool IsConnected { get; }
        Task ConnectAsync(string url);
        Task SendAsync(string message);
        Task DisconnectAsync();
        void Tick();
    }
}

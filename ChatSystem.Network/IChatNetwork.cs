using ChatSystem.Core.Models;

namespace ChatSystem.Network
{
    public interface IChatNetwork
    {
        Task SendMessageAsync(string message, string sender, ChatType type);
        Task RaiseEventAsync(EventType eventType, object data);
        IObservable<(string message, string sender, ChatType type)> OnMessageReceived { get; }
        IObservable<(EventType, object)> OnEventReceived { get; }
        void SimulateDisconnect();
        void SimulateReconnect();
    }
}

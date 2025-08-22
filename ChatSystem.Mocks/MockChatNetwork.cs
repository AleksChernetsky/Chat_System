using ChatSystem.Core.Models;
using ChatSystem.Network;
using System.Reactive.Subjects;

namespace ChatSystem.Mocks
{
    public class MockChatNetwork : IChatNetwork
    {
        public IList<string> ConnectedClients { get; } = new List<string>();

        private readonly Subject<(string, string, ChatType)> _messageSubject = new();
        private readonly Subject<(EventType, object)> _eventSubject = new();
        private bool _isConnected = true;
        private readonly int _minLatencyMs = 100;
        private readonly int _maxLatencyMs = 500;
        public int RandomLatency => new Random().Next(_minLatencyMs, _maxLatencyMs);

        public IObservable<(string message, string sender, ChatType type)> OnMessageReceived => _messageSubject;
        public IObservable<(EventType, object)> OnEventReceived => _eventSubject;

        public async Task SendMessageAsync(string message, string sender, ChatType type)
        {
            if (!ConnectedClients.Contains(sender))
                throw new Exception($"Client '{sender}' is not connected to room.");

            await Task.Delay(RandomLatency);

            if (!_isConnected)
                throw new Exception("Disconnected");

            _messageSubject.OnNext((message, sender, type)); // Broadcast
        }

        public async Task RaiseEventAsync(EventType eventType, object data)
        {
            await Task.Delay(RandomLatency);

            if (!_isConnected)
                throw new Exception("Disconnected");

            _eventSubject.OnNext((eventType, data)); // Broadcast
        }
        public void SimulateDisconnect() => _isConnected = false;
        public void SimulateReconnect() => _isConnected = true;
    }
}
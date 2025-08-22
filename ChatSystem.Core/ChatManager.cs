using ChatSystem.Core.Models;
using ChatSystem.Network;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace ChatSystem.Core
{
    public class ChatManager
    {
        private readonly IChatNetwork _network;
        private readonly IRequestPolicy _retryPolicy;

        private readonly Subject<string> _publicMessages = new();
        private readonly Subject<string> _teamMessages = new();
        private readonly Subject<(EventType, object)> _events = new();

        public ChatManager(IChatNetwork network, IRequestPolicy retryPolicy)
        {
            _network = network;
            _retryPolicy = retryPolicy;
            _network.OnMessageReceived.Subscribe(m =>
            {
                var message = new ChatMessage(m.type, m.sender, m.message);
                var formattedMessage = $"[{message.Type}] {message.Sender}: {message.Text}";
                if (m.type == ChatType.Public)
                {
                    _publicMessages.OnNext(formattedMessage);
                }
                else if (m.type == ChatType.Team)
                {
                    _teamMessages.OnNext(formattedMessage);
                }
            });
            _network.OnEventReceived.Subscribe(_events.OnNext);
        }

        public IObservable<string> PublicMessages => _publicMessages;
        public IObservable<string> TeamMessages => _teamMessages;
        public IObservable<(EventType, object)> Events => _events;

        public Task SendChatMessageAsync(string message, string sender, ChatType chatType)
        {
            return _retryPolicy.ExecuteAsync(async () =>
            {
                try
                {
                    await _network.SendMessageAsync(message, sender, chatType);
                }
                catch (Exception)
                {
                    _network.SimulateReconnect();
                    throw;
                }
            });
        }

        public async Task SendNotificationAsync(EventType eventType, object data)
        {
            await _network.RaiseEventAsync(eventType, data);
        }
    }
}
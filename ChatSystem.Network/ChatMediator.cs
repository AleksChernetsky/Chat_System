using ChatSystem.Core.Models;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace ChatSystem.Network
{
    public class ChatMediator : IChatMediator
    {
        private readonly Subject<object> _bus = new Subject<object>();
        public ChatMediator(IChatNetwork network)
        {
            network.OnMessageReceived.Select(m => new ChatMessage(m.type, m.sender, m.message)).Subscribe(_bus.OnNext);
            network.OnEventReceived.Subscribe(evt => PublishEvent(evt.Item1, evt.Item2));
        }
        public void PublishMessage(string message)
        {
            _bus.OnNext(message);
        }
        public void PublishEvent(EventType eventType, object data)
        {
            _bus.OnNext((eventType, data));
        }
        public IObservable<T> Subscribe<T>()
        {
            return _bus.OfType<T>();
        }
    }
}

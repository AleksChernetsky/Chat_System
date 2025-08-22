using ChatSystem.Core.Models;

namespace ChatSystem.Network
{
    public interface IChatMediator
    {
        // Публикация полученного из сети текстового сообщения
        void PublishMessage(string message);

        // Публикация полученного из сети ивента и данных
        void PublishEvent(EventType eventType, object data);

        // Подписка на поток объектов заданного типа
        IObservable<T> Subscribe<T>();
    }
}

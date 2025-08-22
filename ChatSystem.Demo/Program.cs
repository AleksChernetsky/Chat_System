using ChatSystem.Core;
using ChatSystem.Core.Models;
using ChatSystem.Mocks;
using ChatSystem.Network;
using Microsoft.Extensions.DependencyInjection;

namespace ChatSystem.Demo
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            // Настройка DI
            var services = new ServiceCollection();

            services.AddSingleton<MockChatNetwork>();
            services.AddSingleton<IChatNetwork>(sp => sp.GetRequiredService<MockChatNetwork>());
            services.AddSingleton<IChatMediator, ChatMediator>();
            services.AddSingleton<NotificationBuilder>();
            services.AddSingleton<ChatManager>();
            services.AddSingleton<IRequestPolicy>(sp =>
                    new ExponentialRetryPolicy(
                        maxAttempts: 1,
                        initialDelay: TimeSpan.Zero,
                        backoffFactor: 1.0
                    ));
            var provider = services.BuildServiceProvider();

            // Настройка сервисов
            var mediator = provider.GetRequiredService<IChatMediator>();
            var chatManager = provider.GetRequiredService<ChatManager>();
            var builder = provider.GetRequiredService<NotificationBuilder>();
            var network = provider.GetRequiredService<MockChatNetwork>();
            var policy = provider.GetRequiredService<IRequestPolicy>();

            network.ConnectedClients.Add("Player1");
            network.ConnectedClients.Add("Player2");

            // Подписка через медиатор на входящие сообщения и ивенты
            mediator.Subscribe<ChatMessage>().Subscribe(message => Console.WriteLine($"[{message.Type}] {message.Sender}: {message.Text}"));
            mediator.Subscribe<(EventType, object)>()
                .Subscribe(evt =>
                {
                    var (type, data) = evt;
                    Console.WriteLine($"[Event:{type}] {data}");
                });

            // Отправка сообщений
            await chatManager.SendChatMessageAsync("Hello", "Player1", ChatType.Public);
            await chatManager.SendChatMessageAsync("Hello", "Player2", ChatType.Public);
            await chatManager.SendChatMessageAsync("Hello team", "Player1", ChatType.Team);

            // Отправка ивентов
            var notification = builder
                .SetType(EventType.KillNotification)
                .SetMessage("Player1 killed Player2")
                .Build();
            await chatManager.SendNotificationAsync(notification.Item1, notification.Item2);

            Console.ReadKey();
        }
    }
}

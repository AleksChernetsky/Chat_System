using ChatSystem.Core;
using ChatSystem.Core.Models;
using ChatSystem.Mocks;
using ChatSystem.Network;
using Moq;
using System.Reactive.Subjects;

namespace ChatSystem.Tests
{
    [TestFixture]
    public class ChatManagerTests
    {
        private Mock<IChatNetwork> _mockNetwork;
        private IRequestPolicy _retryPolicy;
        private ChatManager _manager;
        private ChatMediator _mediator;

        [SetUp]
        public void SetUp()
        {
            _mockNetwork = new Mock<IChatNetwork>();
            _mockNetwork.SetupGet(n => n.OnMessageReceived).Returns(new Subject<(string, string, ChatType)>());
            _mockNetwork.SetupGet(n => n.OnEventReceived).Returns(new Subject<(EventType, object)>());

            _mediator = new ChatMediator(_mockNetwork.Object);

            _retryPolicy = new ExponentialRetryPolicy(
                maxAttempts: 2,
                initialDelay: TimeSpan.Zero,
                backoffFactor: 1.0
            );
        }

        [Test]
        public async Task SendMessageAsync_CallsNetworkSend_AndBroadcasts()
        {
            // Arrange
            var messageSubject = new Subject<(string, string, ChatType)>();
            _mockNetwork.Setup(n => n.OnMessageReceived).Returns(messageSubject);

            string? receivedMessage = null;
            _manager = new ChatManager(_mockNetwork.Object, _retryPolicy);
            _manager.PublicMessages.Subscribe(msg => receivedMessage = msg);

            _mockNetwork.Setup(n => n.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ChatType>()))
                       .Returns(Task.CompletedTask)
                       .Callback<string, string, ChatType>((msg, sender, type) =>
                       {
                           messageSubject.OnNext((msg, sender, type)); // Simulate broadcast
                       });

            // Act
            await _manager.SendChatMessageAsync("Hello", "Player1", ChatType.Public);

            // Assert
            _mockNetwork.Verify(n => n.SendMessageAsync("Hello", "Player1", ChatType.Public), Times.Once());
            Assert.That(receivedMessage, Is.EqualTo("[Public] Player1: Hello"));
        }

        [Test]
        public async Task SendNotificationAsync_WithBuilder_CallsRaiseEvent()
        {
            // Arrange
            var eventSubject = new Subject<(EventType, object)>();
            _mockNetwork.Setup(n => n.OnEventReceived).Returns(eventSubject);
            (EventType, object) receivedEvent = default;
            _manager = new ChatManager(_mockNetwork.Object, _retryPolicy);
            _manager.Events.Subscribe(ev => receivedEvent = ev);

            _mockNetwork.Setup(n => n.RaiseEventAsync(It.IsAny<EventType>(), It.IsAny<object>()))
                       .Returns(Task.CompletedTask)
                       .Callback<EventType, object>((type, data) =>
                           eventSubject.OnNext((type, data)));

            var builder = new NotificationBuilder()
                .SetType(EventType.KillNotification)
                .SetMessage("Player1 killed Player2");
            var notification = builder.Build();

            // Act
            await _manager.SendNotificationAsync(notification.Item1, notification.Item2);

            // Assert
            _mockNetwork.Verify(n => n.RaiseEventAsync(EventType.KillNotification, "Player1 killed Player2"), Times.Once());
            Assert.That(receivedEvent.Item1, Is.EqualTo(EventType.KillNotification));
            Assert.That(receivedEvent.Item2, Is.EqualTo("Player1 killed Player2"));
        }

        [Test]
        public async Task SendMessageAsync_OnDisconnect_RetriesAfterReconnect()
        {
            // Arrange
            var messageSubject = new Subject<(string, string, ChatType)>();
            _mockNetwork.Setup(n => n.OnMessageReceived).Returns(messageSubject);

            _mockNetwork.SetupSequence(n => n.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ChatType>()))
                       .ThrowsAsync(new Exception("Disconnected"))
                       .Returns(Task.CompletedTask);

            _mockNetwork.Setup(n => n.SimulateReconnect()).Callback(() => { });
            _manager = new ChatManager(_mockNetwork.Object, _retryPolicy);

            // Act
            await _manager.SendChatMessageAsync("Hello", "Player1", ChatType.Public);

            // Assert
            _mockNetwork.Verify(n => n.SimulateReconnect(), Times.Once());
            _mockNetwork.Verify(n => n.SendMessageAsync("Hello", "Player1", ChatType.Public), Times.Exactly(2)); // Initial + retry
        }

        [Test]
        public async Task SendMessageAsync_BroadcastsOnlyToTeamSubscribers()
        {
            // Arrange
            var messageSubject = new Subject<(string, string, ChatType)>();
            _mockNetwork.SetupGet(n => n.OnMessageReceived).Returns(messageSubject);

            string? publicMessage = null;
            string? teamMessage = null;

            _manager = new ChatManager(_mockNetwork.Object, _retryPolicy);
            _manager.PublicMessages.Subscribe(m => publicMessage = m);
            _manager.TeamMessages.Subscribe(m => teamMessage = m);

            _mockNetwork
                .Setup(n => n.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ChatType>()))
                .Returns(Task.CompletedTask)
                .Callback<string, string, ChatType>((msg, sender, type) =>
                    messageSubject.OnNext((msg, sender, type)));

            // Act
            await _manager.SendChatMessageAsync("Hello team", "Player1", ChatType.Team);

            // Assert
            _mockNetwork.Verify(n => n.SendMessageAsync("Hello team", "Player1", ChatType.Team), Times.Once());
            Assert.That(publicMessage, Is.Null);
            Assert.That(teamMessage, Is.EqualTo("[Team] Player1: Hello team"));
        }

        [Test]
        public async Task SendMessageAsync_Broadcast_ToMultipleClients()
        {
            // Arrange
            var network = new MockChatNetwork();
            network.ConnectedClients.Add("Player1");
            network.ConnectedClients.Add("Player2");

            var policy = new ExponentialRetryPolicy(1, TimeSpan.Zero, 1.0);
            var player1 = new ChatManager(network, policy);
            var player2 = new ChatManager(network, policy);

            string? player1msg = null;
            string? player2msg = null;
            player1.PublicMessages.Subscribe(m => player1msg = m);
            player2.PublicMessages.Subscribe(m => player2msg = m);

            // Act
            await player1.SendChatMessageAsync("Hello", "Player1", ChatType.Public);

            // Assert
            Assert.That(player1msg, Is.EqualTo("[Public] Player1: Hello"));
            Assert.That(player2msg, Is.EqualTo("[Public] Player1: Hello"));
        }

        [Test]
        public void SendMessageAsync_UnconnectedClient_ThrowsException()
        {
            // Arrange
            var network = new MockChatNetwork();
            network.ConnectedClients.Add("Player1");

            var policy = new ExponentialRetryPolicy(1, TimeSpan.Zero, 1.0);
            var player1 = new ChatManager(network, policy);

            // Act & Assert
            var ex = Assert.ThrowsAsync<Exception>(async () =>
                await player1.SendChatMessageAsync("Hello", "Player2", ChatType.Public));
            Assert.That(ex!.Message, Is.EqualTo("Client 'Player2' is not connected to room."));
        }

        [Test]
        public void Network_OnEventReceived_ShouldForwardEventPairs()
        {
            // Arrange
            var subj = new Subject<(EventType, object)>();
            _mockNetwork.SetupGet(n => n.OnEventReceived).Returns(subj);

            _mediator = new ChatMediator(_mockNetwork.Object);

            (EventType, object)? received = null;
            _mediator.Subscribe<(EventType, object)>()
                     .Subscribe(e => received = e);

            // Act
            subj.OnNext((EventType.MatchStart, 123));

            // Assert
            Assert.That(received?.Item1, Is.EqualTo(EventType.MatchStart));
            Assert.That(received?.Item2, Is.EqualTo(123));
        }
    }
}
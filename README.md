## Структура и зависимости
**Вся логика в нескольких проектах:**
- **Core** (ChatManager, NotificationBuilder)
- **Network** (интерфейсы IChatNetwork, IChatMediator, модель ChatMessage)
- **Mocks** (MockChatNetwork эмулирует сеть)
- **Demo** (консольное приложение с DI)
- **Tests** (NUnit + Moq для всех компонентов)
- **DI** через Microsoft.Extensions.DependencyInjection: все сервисы и мок-сеть инжектятся

## ChatManager & retry‑policy
- **ChatManager** — единственная точка отправки: вызывается SendChatMessageAsync или SendNotificationAsync
- **Логика повторных попыток** вынесена в IRequestPolicy (реализация ExponentialRetryPolicy)
	- в конструкторе настраивается максимальное число попыток, задержка и экспоненциальный рост

## Mediator‑паттерн
- **ChatMediator** подписывается на два потока из сети:
	- (string, sender, ChatType) → конвертирует в ChatMessage и пушит в шину _bus- 
	- (EventType, object) → сразу в _bus
- Любой модуль (UI/демо) берёт IChatMediator и вызывет Subscribe<ChatMessage>() или Subscribe<(EventType,object)>() — не нужно напрямую знать про сеть

## MockChatNetwork (имитация сети)
- Генерирует рандомную задержку (100–500 ms)
- Хранит ConnectedClients
- Проверяет подключён ли отправитель и выбрасывает Disconnected при падении
- Методы SimulateDisconnect(), SimulateReconnect() эмулируют обрыв/восстановление сети
- После успешного await Task.Delay(latency) пушит данные в единый Subject для рассылки

## NotificationBuilder
- Builder‑паттерн: SetType(...), SetMessage(...), Build() → собираем объект (пара (EventType, object)) для отправки уведомлений

## Тесты
- **SendMessageAsync_CallsNetworkSend_AndBroadcasts**
	- Проверяет, что при успешной отправке вызывается SendMessageAsync у сети, а потом сообщение формируется и попадает в PublicMessages.
- **SendNotificationAsync_WithBuilder_CallsRaiseEvent**
	- Убеждается, что NotificationBuilder собирает нужный (EventType, data), RaiseEventAsync вызывается и событие приходит во внешний поток Events.
- **SendMessageAsync_OnDisconnect_RetriesAfterReconnect**
	- Симулирует первый сбой (Disconnected), проверяет вызов SimulateReconnect() и повторную отправку через retry‑политику.
- **SendMessageAsync_BroadcastsOnlyToTeamSubscribers**
	- Проверяет, что при ChatType.Team сообщение не попадает в PublicMessages, а только в TeamMessages.
- **SendMessageAsync_Broadcast_ToMultipleClients**
	- Убеждается, что два разных ChatManager, работающие с одним MockChatNetwork, получают одно и то же публичное сообщение.
- **SendMessageAsync_UnconnectedClient_ThrowsException**
	- Проверяет, что незарегистрированный в ConnectedClients отправитель сразу же получает исключение.
- **Network_OnEventReceived_ShouldForwardEventPairs**
	- Убеждается, что приход кортежа (EventType, object) из сети через OnEventReceived правильно отрабатывается ChatMediator и доходит до подписчиков.

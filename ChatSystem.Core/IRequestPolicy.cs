namespace ChatSystem.Core
{
    public interface IRequestPolicy
    {
        // Выполнить переданную асинхронную операцию с политикой retry
        Task ExecuteAsync(Func<Task> operation);
    }
}

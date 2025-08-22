namespace ChatSystem.Core
{
    public class ExponentialRetryPolicy : IRequestPolicy
    {
        private readonly int _maxAttempts;
        private readonly TimeSpan _initialDelay;
        private readonly double _backoffFactor;

        public ExponentialRetryPolicy(int maxAttempts, TimeSpan initialDelay, double backoffFactor)
        {
            _maxAttempts = maxAttempts;
            _initialDelay = initialDelay;
            _backoffFactor = backoffFactor;
        }

        public async Task ExecuteAsync(Func<Task> operation)
        {
            var delay = _initialDelay;
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    await operation();
                    return;
                }
                catch (Exception ex) when (attempt < _maxAttempts)
                {
                    Console.WriteLine($"[Retry] попытка {attempt} не удалась ({ex.Message}), ждём {delay.TotalMilliseconds}ms");
                    await Task.Delay(delay);
                    delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * _backoffFactor);
                }
            }
        }
    }
}

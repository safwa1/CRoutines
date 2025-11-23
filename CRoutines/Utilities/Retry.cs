namespace CRoutines.Utilities;

public static class Retry
{
    public static async Task<T> Execute<T>(
        Func<Task<T>> operation,
        int maxAttempts = 3,
        TimeSpan? delayBetweenAttempts = null)
    {
        var attempts = 0;
        while (true)
        {
            try
            {
                return await operation();
            }
            catch when (++attempts < maxAttempts)
            {
                if (delayBetweenAttempts.HasValue)
                    await Task.Delay(delayBetweenAttempts.Value);
            }
        }
    }

    public static Task Execute(
        Func<Task> operation,
        int maxAttempts = 3,
        TimeSpan? delayBetweenAttempts = null)
        => Execute(async () => { await operation(); return 0; }, maxAttempts, delayBetweenAttempts);
}
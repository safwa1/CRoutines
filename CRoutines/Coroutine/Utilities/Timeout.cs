namespace CRoutines.Coroutine.Utilities;

public static class Timeout
{
    public static async Task<T> WithTimeout<T>(TimeSpan timeout, Func<Task<T>> operation)
    {
        using var cts = new CancellationTokenSource(timeout);
        var task = operation();
        
        if (await Task.WhenAny(task, Task.Delay(System.Threading.Timeout.Infinite, cts.Token)) == task)
            return await task;
        
        throw new TimeoutException($"Operation timed out after {timeout}");
    }

    public static async Task WithTimeout(TimeSpan timeout, Func<Task> operation)
    {
        await WithTimeout(timeout, async () => { await operation(); return 0; });
    }
}
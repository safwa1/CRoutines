namespace CRoutines.Dispatchers;

/// <summary>
/// Executes immediately on current thread (like Dispatchers.Unconfined)
/// </summary>
public sealed class UnconfinedDispatcher : ICoroutineDispatcher
{
    public Task Dispatch(Func<CancellationToken, Task> work, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) 
            return Task.FromCanceled(ct);
        
        try
        {
            return work(ct) ?? Task.CompletedTask;
        }
        catch (Exception ex)
        {
            return Task.FromException(ex);
        }
    }

    public static UnconfinedDispatcher Instance { get; } = new();
}
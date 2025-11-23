using CRoutines.Contexts;
using CRoutines.Core;
using CRoutines.Dispatchers;

namespace CRoutines;

public static class Coroutines
{
    public static async Task RunBlocking(
        Func<CoroutineScope, Task> block,
        ICoroutineDispatcher? dispatcher = null)
    {
        using var scope = new CoroutineScope(dispatcher);
        try
        {
            await block(scope);
            await scope.JoinAll();
        }
        catch (Exception ex)
        {
            CoroutineExceptionHandler.Current?.Handle(ex);
            throw;
        }
    }

    public static CoroutineScope GlobalScope(ICoroutineDispatcher? dispatcher = null)
        => CoroutineScopeCache.GetOrCreate(dispatcher ?? DefaultDispatcher.Instance);
}
using CRoutines.Coroutine.Contexts;
using CRoutines.Coroutine.Core;
using CRoutines.Coroutine.Dispatchers;

namespace CRoutines.Coroutine;

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
        => new(dispatcher);
}
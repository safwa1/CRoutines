namespace CRoutines.Coroutine.Dispatchers;

public interface ICoroutineDispatcher
{
    Task Dispatch(Func<CancellationToken, Task> work, CancellationToken ct);
}
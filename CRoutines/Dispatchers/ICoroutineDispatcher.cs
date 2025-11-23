namespace CRoutines.Dispatchers;

public interface ICoroutineDispatcher
{
    Task Dispatch(Func<CancellationToken, Task> work, CancellationToken ct);
}
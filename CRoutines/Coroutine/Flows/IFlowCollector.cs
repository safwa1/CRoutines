namespace CRoutines.Coroutine.Flows;

public interface IFlowCollector<in T>
{
    ValueTask Emit(T value, CancellationToken ct = default);
}
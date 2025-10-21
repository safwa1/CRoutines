using CRoutines.Coroutine.Flows;

namespace CRoutines;

public static partial class Prelude
{
    public static IAsyncEnumerable<T> FlowOf<T>(
            Func<IFlowCollector<T>, CancellationToken, Task> block,
            CancellationToken ct = default
        ) => Flow.Create(block,ct );
    
    public static IAsyncEnumerable<T> FlowOf<T>(params T[] items) => Flow.Of(items);
}
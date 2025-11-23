using System.Runtime.CompilerServices;
using CRoutines.Flows;

namespace CRoutines;

public static partial class Prelude
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IAsyncEnumerable<T> FlowOf<T>(
            Func<IFlowCollector<T>, CancellationToken, Task> block,
            CancellationToken ct = default
        ) => Flow.Create(block, ct);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IAsyncEnumerable<T> FlowOf<T>(params T[] items) => Flow.Of(items);
}
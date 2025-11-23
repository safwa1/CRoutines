using CRoutines.Core;
using CRoutines.Dispatchers;

namespace CRoutines.Contexts;

public sealed class CoroutineContext
{
    public Job Job { get; }
    public CancellationToken CancellationToken => Job.Cancellation.Token;
    public ICoroutineDispatcher Dispatcher { get; }

    internal CoroutineContext(Job job, ICoroutineDispatcher dispatcher)
    {
        Job = job;
        Dispatcher = dispatcher;
    }

    public CoroutineContext WithDispatcher(ICoroutineDispatcher newDispatcher)
        => new(Job, newDispatcher);
}
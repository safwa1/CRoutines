using System.Collections.Concurrent;
using CRoutines.Coroutine.Core;
using CRoutines.Coroutine.Helpers;

namespace CRoutines.Coroutine.Flows;

public sealed class MutableSharedFlow<T> : ISharedFlow<T>
{
    private readonly ConcurrentDictionary<int, Func<T, Task>> _subscribers = new();
    private int _nextId;

    public IDisposable Subscribe(Func<T, Task> collector)
    {
        var id = Interlocked.Increment(ref _nextId);
        _subscribers[id] = collector;
        return new ActionDisposable(() => _subscribers.TryRemove(id, out _));
    }

    public async Task Emit(T value)
    {
        foreach (var subscriber in _subscribers.Values)
        {
            try
            {
                await subscriber(value);
            }
            catch (Exception ex)
            {
                CoroutineExceptionHandler.Current?.Handle(ex);
            }
        }
    }
}


namespace CRoutines.Coroutine.Flows;

public sealed class MutableStateFlow<T> : ISharedFlow<T>
{
    private readonly MutableSharedFlow<T> _flow = new();
    private T _value;
    
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif

    public T Value
    {
        get
        {
            lock (_lock) return _value;
        }
        set
        {
            lock (_lock) _value = value;
            _ = _flow.Emit(value);
        }
    }

    public MutableStateFlow(T initial) => _value = initial;

    public IDisposable Subscribe(Func<T, Task> collector)
    {
        var sub = _flow.Subscribe(collector);
        _ = collector(Value); // Emit current value immediately
        return sub;
    }

    public async Task Update(Func<T, T> transform)
    {
        T newValue;
        lock (_lock)
        {
            newValue = transform(_value);
            _value = newValue;
        }

        await _flow.Emit(newValue);
    }
}
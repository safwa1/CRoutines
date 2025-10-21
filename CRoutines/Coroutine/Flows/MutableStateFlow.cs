namespace CRoutines.Coroutine.Flows;

public sealed class MutableStateFlow<T> : ISharedFlow<T>
{
    private readonly MutableSharedFlow<T> _flow = new();
    private T _value;
    private readonly Lock _lock = new();

    public T Value
    {
        get { lock (_lock) return _value; }
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
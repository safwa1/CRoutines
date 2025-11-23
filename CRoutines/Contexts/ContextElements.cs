namespace CRoutines.Contexts;

/// <summary>
/// Context element for coroutine name
/// </summary>
public class CoroutineName
{
    public string Name { get; }

    public CoroutineName(string name)
    {
        Name = name;
    }

    public override string ToString() => Name;
}

/// <summary>
/// Context element for coroutine ID
/// </summary>
public class CoroutineId
{
    private static long _nextId = 0;
    
    public long Id { get; }

    public CoroutineId() : this(Interlocked.Increment(ref _nextId))
    {
    }

    public CoroutineId(long id)
    {
        Id = id;
    }

    public override string ToString() => $"#{Id}";
}

/// <summary>
/// Extensions for context elements
/// </summary>
public static class ContextElementExtensions
{
    private static readonly AsyncLocal<CoroutineName?> _coroutineName = new();
    private static readonly AsyncLocal<CoroutineId?> _coroutineId = new();

    /// <summary>
    /// Gets or sets the current coroutine name
    /// </summary>
    public static CoroutineName? CurrentCoroutineName
    {
        get => _coroutineName.Value;
        set => _coroutineName.Value = value;
    }

    /// <summary>
    /// Gets or sets the current coroutine ID
    /// </summary>
    public static CoroutineId? CurrentCoroutineId
    {
        get => _coroutineId.Value;
        set => _coroutineId.Value = value;
    }

    /// <summary>
    /// Runs a block with a coroutine name
    /// </summary>
    public static async Task<T> WithName<T>(string name, Func<Task<T>> block)
    {
        var previous = CurrentCoroutineName;
        try
        {
            CurrentCoroutineName = new CoroutineName(name);
            return await block();
        }
        finally
        {
            CurrentCoroutineName = previous;
        }
    }

    /// <summary>
    /// Runs a block with a coroutine name (void)
    /// </summary>
    public static async Task WithName(string name, Func<Task> block)
    {
        await WithName<object?>(name, async () =>
        {
            await block();
            return null;
        });
    }
}

using CRoutines.Contexts;

namespace CRoutines.Core;

/// <summary>
/// Handler for uncaught coroutine exceptions
/// </summary>
public sealed class CoroutineExceptionHandler
{
    private static readonly AsyncLocal<CoroutineExceptionHandler?> _current = new();
    
    /// <summary>
    /// Current exception handler for this async context
    /// </summary>
    public static CoroutineExceptionHandler? Current 
    { 
        get => _current.Value;
        set => _current.Value = value;
    }

    private readonly List<Action<CoroutineContext?, Exception>> _handlers;

    public CoroutineExceptionHandler()
    {
        _handlers = new List<Action<CoroutineContext?, Exception>>();
    }

    public CoroutineExceptionHandler(Action<Exception> handler)
    {
        _handlers = new List<Action<CoroutineContext?, Exception>>
        {
            (_, ex) => handler(ex)
        };
    }

    public CoroutineExceptionHandler(Action<CoroutineContext?, Exception> handler)
    {
        _handlers = new List<Action<CoroutineContext?, Exception>> { handler };
    }

    /// <summary>
    /// Adds a handler to the chain
    /// </summary>
    public CoroutineExceptionHandler AddHandler(Action<CoroutineContext?, Exception> handler)
    {
        _handlers.Add(handler);
        return this;
    }

    /// <summary>
    /// Adds a simple handler to the chain
    /// </summary>
    public CoroutineExceptionHandler AddHandler(Action<Exception> handler)
    {
        _handlers.Add((_, ex) => handler(ex));
        return this;
    }

    /// <summary>
    /// Handles exception using all registered handlers
    /// </summary>
    public void Handle(CoroutineContext? context, Exception ex)
    {
        foreach (var handler in _handlers)
        {
            try
            {
                handler(context, ex);
            }
            catch
            {
                // Swallow handler exceptions to prevent cascading failures
            }
        }
    }

    /// <summary>
    /// Handles exception (backward compatibility)
    /// </summary>
    public void Handle(Exception ex)
    {
        Handle(null, ex);
    }

    /// <summary>
    /// Creates a logging handler
    /// </summary>
    public static CoroutineExceptionHandler Logging()
        => new((ctx, ex) => 
        {
            var name = ContextElementExtensions.CurrentCoroutineName?.Name ?? "Unknown";
            Console.WriteLine($"[CoroutineException] [{name}] {ex}");
        });

    /// <summary>
    /// Creates a handler that logs and rethrows
    /// </summary>
    public static CoroutineExceptionHandler LoggingAndRethrow()
    {
        var handler = Logging();
        handler.AddHandler((_, ex) => throw ex);
        return handler;
    }

    /// <summary>
    /// Creates a custom handler
    /// </summary>
    public static CoroutineExceptionHandler Custom(Action<CoroutineContext?, Exception> handler)
        => new(handler);

    /// <summary>
    /// Creates a filtered handler (only handles specific exception types)
    /// </summary>
    public static CoroutineExceptionHandler Filtered<TException>(Action<TException> handler)
        where TException : Exception
        => new((_, ex) =>
        {
            if (ex is TException typedEx)
            {
                handler(typedEx);
            }
        });

    /// <summary>
    /// Combines multiple handlers
    /// </summary>
    public static CoroutineExceptionHandler Combine(params CoroutineExceptionHandler[] handlers)
    {
        var combined = new CoroutineExceptionHandler();
        foreach (var handler in handlers)
        {
            foreach (var h in handler._handlers)
            {
                combined._handlers.Add(h);
            }
        }
        return combined;
    }

    /// <summary>
    /// Installs this handler on the current async context
    /// Similar to Kotlin's installOn - returns the previous handler
    /// </summary>
    public CoroutineExceptionHandler? InstallOn()
    {
        var previous = Current;
        Current = this;
        return previous;
    }

    /// <summary>
    /// Runs code with this handler installed
    /// Automatically restores the previous handler when done
    /// </summary>
    public async Task<T> WithHandler<T>(Func<Task<T>> block)
    {
        var previous = Current;
        try
        {
            Current = this;
            return await block();
        }
        finally
        {
            Current = previous;
        }
    }

    /// <summary>
    /// Runs code with this handler installed (void version)
    /// </summary>
    public async Task WithHandler(Func<Task> block)
    {
        var previous = Current;
        try
        {
            Current = this;
            await block();
        }
        finally
        {
            Current = previous;
        }
    }

    /// <summary>
    /// Creates a scoped handler that will be cleaned up automatically
    /// Usage: using (handler.Install()) { ... }
    /// </summary>
    public IDisposable Install()
    {
        var previous = Current;
        Current = this;
        return new HandlerScope(previous);
    }

    private class HandlerScope : IDisposable
    {
        private readonly CoroutineExceptionHandler? _previous;

        public HandlerScope(CoroutineExceptionHandler? previous)
        {
            _previous = previous;
        }

        public void Dispose()
        {
            Current = _previous;
        }
    }
}
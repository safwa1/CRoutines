namespace CRoutines.Coroutine.Core;

public sealed class CoroutineExceptionHandler
{
    public static CoroutineExceptionHandler? Current { get; set; }

    private readonly Action<Exception> _handler;

    public CoroutineExceptionHandler(Action<Exception> handler)
    {
        _handler = handler;
    }

    public void Handle(Exception ex)
    {
        try
        {
            _handler(ex);
        }
        catch
        {
            // Swallow handler exceptions
        }
    }

    public static CoroutineExceptionHandler Logging()
        => new(ex => Console.WriteLine($"[CoroutineException] {ex}"));
}
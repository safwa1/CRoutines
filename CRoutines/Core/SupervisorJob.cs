namespace CRoutines.Core;

/// <summary>
/// SupervisorJob isolates child failures - they don't cancel siblings or parent
/// </summary>
public sealed class SupervisorJob : Job
{
    public SupervisorJob(Job? parent = null) : base(parent) { }

    protected override void HandleChildCancellation(Job child)
    {
        // Supervisor doesn't propagate child cancellation
    }

    protected override void HandleChildException(Exception ex)
    {
        // Log but don't propagate
        CoroutineExceptionHandler.Current?.Handle(ex);
    }
}
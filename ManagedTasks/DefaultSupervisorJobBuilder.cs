namespace ManagedTasks;

public sealed class DefaultSupervisorJobBuilder : SupervisorJobBuilder
{
    public override SupervisorJob Build()
    {
        return new SupervisorJob(Tasks, Strategy, MaxRetries, RetryDelay);
    }
    
    public static DefaultSupervisorJobBuilder Create() => new();
}
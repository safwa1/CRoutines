namespace ManagedTasks;

public abstract class SupervisorJobBuilder
{
    protected SupervisionStrategy Strategy = SupervisionStrategy.RestartFailed;
    protected int MaxRetries = 3;
    protected TimeSpan RetryDelay = TimeSpan.FromSeconds(1);
    protected readonly List<FlowTask> Tasks = new();

    public SupervisorJobBuilder WithStrategy(SupervisionStrategy strategy)
    {
        Strategy = strategy;
        return this;
    }

    public SupervisorJobBuilder WithMaxRetries(int retries)
    {
        MaxRetries = retries;
        return this;
    }

    public SupervisorJobBuilder WithRetryDelay(TimeSpan delay)
    {
        RetryDelay = delay;
        return this;
    }

    public SupervisorJobBuilder AddTask(FlowTask task)
    {
        Tasks.Add(task);
        return this;
    }

    public SupervisorJobBuilder AddTasks(params FlowTask[] tasks)
    {
        Tasks.AddRange(tasks);
        return this;
    }
    
    public SupervisorJobBuilder RestartFailed(int maxRetries = 3, TimeSpan? retryDelay = null)
    {
        Strategy = SupervisionStrategy.RestartFailed;
        MaxRetries = maxRetries;
        RetryDelay = retryDelay ?? TimeSpan.FromSeconds(1);
        return this;
    }

    public abstract SupervisorJob Build();
}
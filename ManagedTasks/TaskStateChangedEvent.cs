namespace ManagedTasks;

public sealed record TaskStateChangedEvent(
    string TaskName,
    TaskState NewState,
    DateTime Timestamp,
    double Progress = 0,
    string? ErrorMessage = null,
    Exception? Exception = null,
    TimeSpan? Duration = null
);
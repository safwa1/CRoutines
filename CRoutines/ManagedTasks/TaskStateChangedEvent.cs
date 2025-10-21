namespace CRoutines.ManagedTasks;

public sealed record TaskStateChangedEvent(
    string TaskName,
    TaskState NewState,
    DateTime Timestamp,
    double Progress = 0,
    string? ErrorMessage = null,
    TimeSpan? Duration = null
);
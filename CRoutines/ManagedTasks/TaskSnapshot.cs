namespace CRoutines.ManagedTasks;

public sealed record TaskSnapshot(
    string Name,
    TaskState State,
    TaskPriority Priority,
    DateTime CreatedAt,
    TimeSpan? Duration,
    double Progress
);
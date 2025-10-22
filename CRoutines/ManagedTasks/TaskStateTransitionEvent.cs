namespace CRoutines.ManagedTasks;

public sealed record TaskStateTransitionEvent(
    string Name,
    TaskState PreviousState,
    TaskState NewState,
    DateTime Timestamp,
    Exception? Exception = null
);
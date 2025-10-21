namespace CRoutines.Coroutine.Utilities;

public static class Select
{
    public static async Task<T> From<T>(params Func<CancellationToken, Task<T>>[] choices)
    {
        using var cts = new CancellationTokenSource();
        var tasks = choices.Select(c => c(cts.Token)).ToArray();
        var completed = await Task.WhenAny(tasks);
        cts.Cancel();
        return await completed;
    }
}
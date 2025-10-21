using System.Collections.Concurrent;
using CRoutines.Coroutine.Contexts;
using CRoutines.Coroutine.Dispatchers;

namespace CRoutines.Coroutine.Core;

public static class CoroutineScopeCache
{
    private static readonly ConcurrentBag<CoroutineScope> Cache = [];

    public static CoroutineScope GetOrCreate(ICoroutineDispatcher dispatcher, Job? parentJob = null)
    {
        foreach (var scope in Cache)
        {
            if (!scope.Job.IsCancelled && scope.Job.Parent == parentJob)
            {
                Console.WriteLine("From cache");
                return scope;
            }
        }

        var newScope = new CoroutineScope(dispatcher, parentJob);
        Cache.Add(newScope);
        return newScope;
    }
    
    public static CoroutineScope Create(ICoroutineDispatcher? dispatcher = null, Job? parentJob = null)
    {
        var newScope = new CoroutineScope(dispatcher, parentJob);
        Cache.Add(newScope);
        return newScope;
    }
    
    public static void Add(CoroutineScope scope)
    {
        Cache.Add(scope);
    }
    
    public static void Remove(CoroutineScope scopeToRemove)
    {
        var remaining = Cache.Where(s => s != scopeToRemove).ToList();

        // Empty the bag
        while (!Cache.IsEmpty)
            Cache.TryTake(out _);

        // Re-add remaining scopes
        foreach (var scope in remaining)
            Cache.Add(scope);

        // Cancel and dispose the removed scope
        scopeToRemove.Cancel();
        scopeToRemove.Dispose();
    }
    
    public static void ClearAll()
    {
        // Take a snapshot of valid scopes
        var validScopes = Cache.Where(s => !s.Job.IsCancelled).ToList();

        // Clear the bag
        while (!Cache.IsEmpty)
            Cache.TryTake(out _);

        // Cancel and dispose everything we had
        foreach (var scope in validScopes)
        {
            scope.Cancel();
            scope.Dispose();
        }

        validScopes.Clear();
    }
}

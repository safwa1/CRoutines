using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using CRoutines.Coroutine.Contexts;
using CRoutines.Coroutine.Dispatchers;

namespace CRoutines.Coroutine.Core;

public static class CoroutineScopeCache
{
    private static readonly ConcurrentDictionary<ScopeKey, WeakReference<CoroutineScope>> Cache = new();
    private static readonly Timer CleanupTimer;

    static CoroutineScopeCache()
    {
        // Cleanup dead references every 30 seconds
        CleanupTimer = new Timer(_ => CleanupDeadReferences(), null, 
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    private record ScopeKey(Type DispatcherType, int DispatcherHash, Job? ParentJob)
    {
        public static ScopeKey Create(ICoroutineDispatcher dispatcher, Job? parentJob)
        {
            return new ScopeKey(
                dispatcher.GetType(),
                RuntimeHelpers.GetHashCode(dispatcher),
                parentJob
            );
        }
    }

    public static CoroutineScope GetOrCreate(ICoroutineDispatcher dispatcher, Job? parentJob = null)
    {
        var key = ScopeKey.Create(dispatcher, parentJob);

        while (true)
        {
            if (Cache.TryGetValue(key, out var weakRef))
            {
                if (weakRef.TryGetTarget(out var scope))
                {
                    // Valid scope found
                    if (!scope.Job.IsCancelled && !scope.Job.IsCompleted)
                    {
                        Console.WriteLine("FROM Caching");
                        return scope;
                    }
                    else
                    {
                        // Scope is dead, remove it
                        Cache.TryRemove(key, out _);
                    }
                }
                else
                {
                    // WeakReference is dead, remove it
                    Cache.TryRemove(key, out _);
                }
            }
            else
            {
                // Create new scope
                var newScope = new CoroutineScope(dispatcher, parentJob);
                var newWeakRef = new WeakReference<CoroutineScope>(newScope);
                
                if (Cache.TryAdd(key, newWeakRef))
                {
                    return newScope;
                }
                // Else: another thread added it, try again
            }
        }
    }

    public static void Remove(CoroutineScope scope)
    {
        var keysToRemove = new List<ScopeKey>();
        
        foreach (var kvp in Cache)
        {
            if (kvp.Value.TryGetTarget(out var cachedScope) && ReferenceEquals(cachedScope, scope))
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            if (Cache.TryRemove(key, out _))
            {
                scope.Cancel();
                scope.Dispose();
            }
        }
    }

    public static void ClearAll()
    {
        var scopes = new List<CoroutineScope>();

        // Collect all valid scopes
        foreach (var kvp in Cache)
        {
            if (kvp.Value.TryGetTarget(out var scope))
            {
                scopes.Add(scope);
            }
        }

        // Clear cache
        Cache.Clear();

        // Cancel and dispose all scopes
        foreach (var scope in scopes)
        {
            try
            {
                scope.Cancel();
                scope.Dispose();
            }
            catch
            {
                // Ignore disposal errors
            }
        }
    }

    private static void CleanupDeadReferences()
    {
        var deadKeys = new List<ScopeKey>();

        foreach (var kvp in Cache)
        {
            if (!kvp.Value.TryGetTarget(out var scope) || 
                scope.Job.IsCancelled || 
                scope.Job.IsCompleted)
            {
                deadKeys.Add(kvp.Key);
            }
        }

        foreach (var key in deadKeys)
        {
            Cache.TryRemove(key, out _);
        }
    }

    public static int Count => Cache.Count;
}
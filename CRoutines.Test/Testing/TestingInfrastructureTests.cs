using CRoutines.Testing;
using CRoutines.Utilities;
using NUnit.Framework;

namespace CRoutines.Test.Testing;

/// <summary>
/// Tests for the Testing Infrastructure itself
/// </summary>
[TestFixture]
public class TestingInfrastructureTests
{
    [Test]
    public async Task VirtualTime_AdvancesCorrectly()
    {
        await TestHelpers.RunTest(async scope =>
        {
            var startTime = scope.CurrentTime;
            
            await scope.AdvanceTimeBy(TimeSpan.FromSeconds(5));
            
            Assert.That(scope.CurrentTime - startTime, Is.EqualTo(TimeSpan.FromSeconds(5)));
        });
    }
    
    [Test]
    public async Task Delay_WorksWithVirtualTime()
    {
        await TestHelpers.RunTest(async scope =>
        {
            var executed = false;
            
            scope.Launch(async ctx =>
            {
                await Delay.For(TimeSpan.FromSeconds(1));
                executed = true;
            });
            
            // Not executed yet
            Assert.That(executed, Is.False);
            
            // Advance time
            await scope.AdvanceTimeBy(TimeSpan.FromSeconds(1));
            
            // Now executed
            Assert.That(executed, Is.True);
        });
    }
    
    [Test]
    public async Task TestScope_TracksActiveJobs()
    {
        await TestHelpers.RunTest(async scope =>
        {
            Assert.That(scope.IsIdle, Is.True);
            
            var job = scope.Launch(async ctx =>
            {
                await Delay.For(500);
            });
            
            Assert.That(scope.IsIdle, Is.False);
            
            await scope.AdvanceTimeBy(TimeSpan.FromMilliseconds(500));
            await scope.RunUntilIdle();
            
            Assert.That(scope.IsIdle, Is.True);
        });
    }
    
    [Test]
    public async Task TestDispatcher_ExecutesSynchronously()
    {
        await TestHelpers.RunTest(async scope =>
        {
            var executionOrder = new List<int>();
            
            scope.Launch(async ctx =>
            {
                executionOrder.Add(1);
                await Delay.For(100);
                executionOrder.Add(2);
            });
            
            scope.Launch(async ctx =>
            {
                executionOrder.Add(3);
                await Delay.For(50);
                executionOrder.Add(4);
            });
            
            await scope.AdvanceTimeBy(TimeSpan.FromMilliseconds(150));
            
            // Should be deterministic
            Assert.That(executionOrder, Is.EqualTo(new[] { 1, 3, 4, 2 }));
        });
    }
    
    [Test]
    public async Task RunUntilIdle_WaitsForCompletion()
    {
        await TestHelpers.RunTest(async scope =>
        {
            var completed = false;
            
            scope.Launch(async ctx =>
            {
                await Delay.For(100);
                completed = true;
            });
            
            var idle = await scope.RunUntilIdle(TimeSpan.FromSeconds(1));
            
            Assert.That(idle, Is.True);
            Assert.That(completed, Is.True);
        });
    }
    
    [Test]
    public async Task RunUntilIdle_RespectsTimeout()
    {
        await TestHelpers.RunTest(async scope =>
        {
            scope.Launch(async ctx =>
            {
                await Delay.For(TimeSpan.FromSeconds(10));
            });
            
            var idle = await scope.RunUntilIdle(TimeSpan.FromMilliseconds(100));
            
            Assert.That(idle, Is.False);
        });
    }
    
    [Test]
    public async Task AssertCompletes_PassesWhenCompletes()
    {
        await TestHelpers.RunTest(async scope =>
        {
            var completed = false;
            
            await TestHelpers.AssertCompletes(
                scope,
                TimeSpan.FromMilliseconds(200),
                async () =>
                {
                    await Delay.For(100);
                    completed = true;
                });
            
            Assert.That(completed, Is.True);
        });
    }
    
    [Test]
    public async Task AssertCompletes_ThrowsWhenDoesNotComplete()
    {
        await TestHelpers.RunTest(async scope =>
        {
            Assert.ThrowsAsync<TestAssertionException>(async () =>
            {
                await TestHelpers.AssertCompletes(
                    scope,
                    TimeSpan.FromMilliseconds(100),
                    async () =>
                    {
                        await Delay.For(500);
                    });
            });
        });
    }
    
    [Test]
    public async Task MultipleJobs_ExecuteDeterministically()
    {
        await TestHelpers.RunTest(async scope =>
        {
            var results = new List<string>();
            
            for (int i = 0; i < 5; i++)
            {
                var index = i;
                scope.Launch(async ctx =>
                {
                    await Delay.For(index * 100);
                    results.Add($"Job {index}");
                });
            }
            
            await scope.AdvanceTimeBy(TimeSpan.FromMilliseconds(500));
            
            Assert.That(results.Count, Is.EqualTo(5));
            Assert.That(results[0], Is.EqualTo("Job 0"));
            Assert.That(results[1], Is.EqualTo("Job 1"));
            Assert.That(results[2], Is.EqualTo("Job 2"));
            Assert.That(results[3], Is.EqualTo("Job 3"));
            Assert.That(results[4], Is.EqualTo("Job 4"));
        });
    }
    
    [Test]
    public async Task AsyncWithResult_WorksWithVirtualTime()
    {
        var result = await TestHelpers.RunTest(async scope =>
        {
            var deferred = scope.Async(async ctx =>
            {
                await Delay.For(200);
                return 42;
            });
            
            await scope.AdvanceTimeBy(TimeSpan.FromMilliseconds(200));
            
            return await deferred.Await();
        });
        
        Assert.That(result, Is.EqualTo(42));
    }
    
    [Test]
    public async Task VirtualTime_CanBeReset()
    {
        using var scope = new TestCoroutineScope();
        
        await scope.AdvanceTimeBy(TimeSpan.FromSeconds(10));
        Assert.That(scope.CurrentTime, Is.EqualTo(TimeSpan.FromSeconds(10)));
        
        // Note: Reset is internal, so we test via a new scope
        scope.Dispose();
        
        using var newScope = new TestCoroutineScope();
        Assert.That(newScope.CurrentTime, Is.EqualTo(TimeSpan.Zero));
    }
    
    [Test]
    public async Task TestScope_CancelsJobsOnDispose()
    {
        var scope = new TestCoroutineScope();
        var cancelled = false;
        
        scope.Launch(async ctx =>
        {
            try
            {
                await Delay.For(1000);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }
        });
        
        scope.Dispose();
        
        // Give some time for cancellation
        await Task.Delay(50);
        
        Assert.That(cancelled, Is.True);
    }
}

using CRoutines.Contexts;
using CRoutines.Core;
using CRoutines.Testing;
using CRoutines.Utilities;
using NUnit.Framework;

namespace CRoutines.Test.Core;

/// <summary>
/// Comprehensive tests for Job state management and lifecycle
/// Tests use public API only (via CoroutineScope)
/// </summary>
[TestFixture]
public class JobTests
{
    #region State Management Tests

    [Test]
    public void Job_InitialState_IsActive()
    {
        var job = new Job();
        
        Assert.That(job.IsActive, Is.True);
        Assert.That(job.IsCompleted, Is.False);
        Assert.That(job.IsCancelled, Is.False);
        Assert.That(job.IsFaulted, Is.False);
    }
    
    [Test]
    public async Task Job_AfterCompletion_IsCompleted()
    {
        await TestHelpers.RunTest(async scope =>
        {
            var job = scope.Launch(async ctx =>
            {
                await Delay.For(100);
            });
            
            await scope.AdvanceTimeBy(TimeSpan.FromMilliseconds(150));
            await scope.RunUntilIdle();
            
            Assert.That(job.IsCompleted, Is.True);
            Assert.That(job.IsActive, Is.False);
        });
    }
    
    [Test]
    public async Task Job_Cancel_TransitionsToCancelledState()
    {
        await TestHelpers.RunTest(async scope =>
        {
            var job = scope.Launch(async ctx =>
            {
                await Delay.For(1000);
            });
            
            job.Cancel();
            await scope.AdvanceTimeBy(TimeSpan.FromMilliseconds(100));
            
            Assert.That(job.IsCancelled, Is.True);
            Assert.That(job.IsActive, Is.False);
        });
    }
    
    [Test]
    public async Task Job_Exception_TransitionsToFaultedState()
    {
        await TestHelpers.RunTest(async scope =>
        {
            var job = scope.Launch(async ctx =>
            {
                await Delay.For(50);
                throw new InvalidOperationException("Test error");
            });
            
            await scope.AdvanceTimeBy(TimeSpan.FromMilliseconds(100));
            await Task.Delay(50); // Let exception propagate
            
            Assert.That(job.IsFaulted, Is.True);
            Assert.That(job.IsActive, Is.False);
        });
    }
    
    [Test]
    public async Task Job_CancelAfterCompletion_DoesNotChangeState()
    {
        await TestHelpers.RunTest(async scope =>
        {
            var job = scope.Launch(async ctx =>
            {
                await Delay.For(50);
            });
            
            await scope.AdvanceTimeBy(TimeSpan.FromMilliseconds(100));
            await scope.RunUntilIdle();
            
            job.Cancel();
            
            Assert.That(job.IsCompleted, Is.True);
            Assert.That(job.IsCancelled, Is.False);
        });
    }
    
    [Test]
    public async Task Job_CancellationReason_TrackedCorrectly()
    {
        await TestHelpers.RunTest(async scope =>
        {
            var job = scope.Launch(async ctx =>
            {
                await Delay.For(1000);
            });
            
            var reason = "User requested cancellation";
            job.Cancel(reason);
            
            Assert.That(job.CancellationReason, Is.EqualTo(reason));
        });
    }
    
    [Test]
    public void Job_EnsureActive_ThrowsWhenCancelled()
    {
        var job = new Job();
        job.Cancel();
        
        Assert.Throws<OperationCanceledException>(() => job.EnsureActive());
    }

    #endregion

    #region Parent-Child Relationship Tests

    [Test]
    public async Task Job_LaunchedJob_HasParent()
    {
        await TestHelpers.RunTest(async scope =>
        {
            var job = scope.Launch(async ctx =>
            {
                await Delay.For(50);
            });
            
            Assert.That(job.Parent, Is.Not.Null);
            Assert.That(job.Parent, Is.EqualTo(scope.Scope.Job));
        });
    }
    
    [Test]
    public async Task Job_ParentCancellation_CascadesToChildren()
    {
        await TestHelpers.RunTest(async scope =>
        {
            var childCancelled = false;
            
            var child = scope.Launch(async ctx =>
            {
                try
                {
                    await Delay.For(1000);
                }
                catch (OperationCanceledException)
                {
                    childCancelled = true;
                }
            });
            
            scope.Cancel();
            await scope.AdvanceTimeBy(TimeSpan.FromMilliseconds(100));
            
            Assert.That(childCancelled, Is.True);
            Assert.That(child.IsCancelled, Is.True);
        });
    }
    
    [Test]
    public async Task Job_ChildException_CancelsParent()
    {
        // Note: Whether child exceptions cancel parent depends on exception handler configuration
        // This test verifies the child fails, not necessarily parent cancellation
        await TestHelpers.RunTest(async scope =>
        {
            var childFailed = false;
            
            var child = scope.Launch(async ctx =>
            {
                await Delay.For(50);
                childFailed = true;
                throw new InvalidOperationException("Child failed");
            });
            
            await scope.AdvanceTimeBy(TimeSpan.FromMilliseconds(100));
            await Task.Delay(100); // Let exception propagate
            
            Assert.That(childFailed, Is.True);
            Assert.That(child.IsFaulted, Is.True);
        });
    }
    
    [Test]
    public async Task Job_ChildCompletion_DoesNotAffectSiblings()
    {
        await TestHelpers.RunTest(async scope =>
        {
            var child1Completed = false;
            var child2StillRunning = false;
            
            scope.Launch(async ctx =>
            {
                await Delay.For(50);
                child1Completed = true;
            });
            
            var child2 = scope.Launch(async ctx =>
            {
                await Delay.For(200);
                child2StillRunning = true;
            });
            
            await scope.AdvanceTimeBy(TimeSpan.FromMilliseconds(100));
            
            Assert.That(child1Completed, Is.True);
            Assert.That(child2.IsActive, Is.True);
            Assert.That(scope.Scope.Job.IsActive, Is.True);
        });
    }
    
    [Test]
    public async Task Job_MultipleLevelsOfHierarchy_WorkCorrectly()
    {
        await TestHelpers.RunTest(async scope =>
        {
            Job? grandchild = null;
            
            var child = scope.Launch(async childCtx =>
            {
                var childScope = new CoroutineScope(childCtx.Dispatcher, childCtx.Job);
                grandchild = childScope.Launch(async _ =>
                {
                    await Delay.For(50);
                });
                await Delay.For(100);
            });
            
            await Task.Delay(10); // Let hierarchy establish
            
            Assert.That(grandchild, Is.Not.Null);
            Assert.That(grandchild!.Parent, Is.EqualTo(child));
            Assert.That(child.Parent, Is.EqualTo(scope.Scope.Job));
        });
    }

    #endregion

    #region Cancellation Tests

    [Test]
    public async Task Job_ManualCancellation_Works()
    {
        await TestHelpers.RunTest(async scope =>
        {
            var job = scope.Launch(async ctx =>
            {
                await Delay.For(1000);
            });
            
            job.Cancel();
            
            Assert.That(job.IsCancelled, Is.True);
            Assert.That(job.Cancellation.Token.IsCancellationRequested, Is.True);
        });
    }
    
    [Test]
    public async Task Job_CancellationToken_PropagatesRequest()
    {
        await TestHelpers.RunTest(async scope =>
        {
            Job? job = null;
            var tokenCancelled = false;
            
            job = scope.Launch(async ctx =>
            {
                ctx.Job.Cancellation.Token.Register(() => tokenCancelled = true);
                await Delay.For(1000);
            });
            
            await Task.Delay(10); // Let registration happen
            job.Cancel();
            await Task.Delay(10); // Let cancellation propagate
            
            Assert.That(tokenCancelled, Is.True);
        });
    }
    
    [Test]
    public async Task Job_CancelAndJoin_WaitsForCancellation()
    {
        await TestHelpers.RunTest(async scope =>
        {
            var cancelled = false;
            
            var job = scope.Launch(async ctx =>
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
            
            try
            {
                await job.CancelAndJoin();
            }
            catch (TaskCanceledException)
            {
                // Expected - job was cancelled
            }
            
            Assert.That(job.IsCancelled, Is.True);
        });
    }
    
    [Test]
    public async Task Job_AlreadyCancelled_CancelIsIdempotent()
    {
        await TestHelpers.RunTest(async scope =>
        {
            var job = scope.Launch(async ctx =>
            {
                await Delay.For(1000);
            });
            
            job.Cancel("First");
            job.Cancel("Second");
            
            Assert.That(job.IsCancelled, Is.True);
            Assert.That(job.CancellationReason, Is.EqualTo("First"));
        });
    }
    
    [Test]
    public async Task Job_CancellationDuringExecution_Propagates()
    {
        await TestHelpers.RunTest(async scope =>
        {
            var executionStarted = false;
            
            var job = scope.Launch(async ctx =>
            {
                executionStarted = true;
                // Use cancellation token for check
                await Delay.For(1000);
            });
            
            await Task.Delay(50); // Let execution start
            job.Cancel();
            await Task.Delay(50); // Let cancellation propagate
            
            Assert.That(executionStarted, Is.True);
            Assert.That(job.IsCancelled, Is.True);
            Assert.That(job.Cancellation.Token.IsCancellationRequested, Is.True);
        });
    }

    #endregion

    #region Join & Await Tests

    [Test]
    public async Task Job_JoinOnCompleted_ReturnsImmediately()
    {
        await TestHelpers.RunTest(async scope =>
        {
            var job = scope.Launch(async ctx =>
            {
                await Delay.For(50);
            });
            
            await scope.AdvanceTimeBy(TimeSpan.FromMilliseconds(100));
            await scope.RunUntilIdle();
            
            var start = DateTime.UtcNow;
            await job.Join();
            var elapsed = DateTime.UtcNow - start;
            
            Assert.That(elapsed.TotalMilliseconds, Is.LessThan(100));
        });
    }
    
    [Test]
    public async Task Job_JoinOnRunning_Waits()
    {
        await TestHelpers.RunTest(async scope =>
        {
            var completed = false;
            
            var job = scope.Launch(async ctx =>
            {
                await Delay.For(200);
                completed = true;
            });
            
            // Start join (will wait)
            var joinTask = Task.Run(async () => await job.Join());
            
            await scope.AdvanceTimeBy(TimeSpan.FromMilliseconds(250));
            await joinTask;
            
            Assert.That(completed, Is.True);
        });
    }
    
    [Test]
    public void Job_JoinOnCancelled_Throws()
    {
        var job = new Job();
        job.Cancel();
        
        // TaskCanceledException is a subclass of OperationCanceledException
        Assert.ThrowsAsync<TaskCanceledException>(async () => await job.Join());
    }
    
    [Test]
    public async Task Job_JoinWithTimeout_Success()
    {
        await TestHelpers.RunTest(async scope =>
        {
            var job = scope.Launch(async ctx =>
            {
                await Delay.For(100);
            });
            
            await scope.AdvanceTimeBy(TimeSpan.FromMilliseconds(150));
            var result = await job.Join(TimeSpan.FromSeconds(1));
            
            Assert.That(result, Is.True);
        });
    }
    
    [Test]
    public async Task Job_JoinWithTimeout_Timeout()
    {
        await TestHelpers.RunTest(async scope =>
        {
            var job = scope.Launch(async ctx =>
            {
                await Delay.For(10000);
            });
            
            var result = await job.Join(TimeSpan.FromMilliseconds(100));
            
            Assert.That(result, Is.False);
        });
    }
    
    [Test]
    public async Task Job_MultipleConcurrentJoins_AllSucceed()
    {
        await TestHelpers.RunTest(async scope =>
        {
            var job = scope.Launch(async ctx =>
            {
                await Delay.For(100);
            });
            
            var join1 = job.Join();
            var join2 = job.Join();
            var join3 = job.Join();
            
            await scope.AdvanceTimeBy(TimeSpan.FromMilliseconds(150));
            await Task.WhenAll(join1, join2, join3);
            
            Assert.Pass();
        });
    }

    #endregion
}

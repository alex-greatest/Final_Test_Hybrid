using Final_Test_Hybrid.Models.Steps;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class ExecutionStateManagerTests
{
    [Fact]
    public void TryRemoveError_RemovesMatchingError_AndPreservesOtherOrder()
    {
        var manager = new ExecutionStateManager();
        var first = CreateError(0);
        var second = CreateError(1);
        var third = CreateError(2);

        manager.EnqueueError(first);
        manager.EnqueueError(second);
        manager.EnqueueError(third);

        var removed = manager.TryRemoveError(second);

        Assert.True(removed);
        Assert.Equal(first, manager.DequeueError());
        Assert.Equal(third, manager.DequeueError());
        Assert.Null(manager.DequeueError());
    }

    [Fact]
    public void TryRemoveError_ReturnsFalse_WhenErrorIsAbsent()
    {
        var manager = new ExecutionStateManager();
        var current = CreateError(0);
        var missing = CreateError(1);

        manager.EnqueueError(current);

        var removed = manager.TryRemoveError(missing);

        Assert.False(removed);
        Assert.Equal(current, manager.CurrentError);
    }

    private static StepError CreateError(int columnIndex)
    {
        return new StepError(
            columnIndex,
            $"Step-{columnIndex}",
            "desc",
            "err",
            "source",
            DateTime.UtcNow,
            Guid.NewGuid(),
            FailedStep: null);
    }
}

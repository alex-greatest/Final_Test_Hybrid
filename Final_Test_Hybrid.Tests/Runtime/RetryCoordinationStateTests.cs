using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Coordinator;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class RetryCoordinationStateTests
{
    [Fact]
    public void MarkRequested_SuppressesColumn_UntilCompleted()
    {
        var state = new RetryCoordinationState();

        state.MarkRequested(2);

        Assert.True(state.IsActive);
        Assert.True(state.IsColumnSuppressed(2));

        state.MarkCompleted(2);

        Assert.False(state.IsActive);
        Assert.False(state.IsColumnSuppressed(2));
    }

    [Fact]
    public void MarkRequested_DoesNotDoubleCountSameColumn()
    {
        var state = new RetryCoordinationState();

        state.MarkRequested(1);
        state.MarkRequested(1);
        state.MarkCompleted(1);

        Assert.False(state.IsActive);
        Assert.False(state.IsColumnSuppressed(1));
    }
}

using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution;
using Final_Test_Hybrid.Tests.TestSupport;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class RuntimeTerminalStateTests
{
    [Fact]
    public void FlagsAndNotifications_FollowRealTerminalWindowTransitions()
    {
        var state = new RuntimeTerminalState(TestInfrastructure.CreateDualLogger<RuntimeTerminalState>());
        var notifications = 0;
        state.OnChanged += () => notifications++;

        Assert.False(state.HasTerminalHandshake);

        state.SetCompletionActive(true);
        state.SetCompletionActive(true);
        state.SetPostAskEndActive(true);
        state.SetCompletionActive(false);
        state.SetPostAskEndActive(false);

        Assert.False(state.IsCompletionActive);
        Assert.False(state.IsPostAskEndActive);
        Assert.False(state.HasTerminalHandshake);
        Assert.Equal(4, notifications);
    }
}

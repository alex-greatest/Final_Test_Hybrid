using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Plc;
using Final_Test_Hybrid.Tests.TestSupport;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class PlcRetrySignalFreshnessGuardTests
{
    [Fact]
    public async Task EnsureSignalsFreshAsync_WaitsErrorThenEnd_WhenBothSignalsAreKnownTrue()
    {
        var subscription = TestInfrastructure.CreateSubscription();
        var values = TestInfrastructure.GetSubscriptionValues(subscription);
        var step = new PlcStepStub("DB_VI.Block_Test");
        var errorTag = PlcBlockTagHelper.GetErrorTag(step)!;
        var endTag = PlcBlockTagHelper.GetEndTag(step)!;
        values[errorTag] = true;
        values[endTag] = true;
        var calls = new List<string>();

        await PlcRetrySignalFreshnessGuard.EnsureSignalsFreshAsync(
            step,
            subscription,
            (tag, _, _) =>
            {
                calls.Add(tag);
                values[tag] = false;
                return Task.CompletedTask;
            },
            TimeSpan.FromSeconds(1),
            (_, _, _) => { },
            "test retry",
            CancellationToken.None);

        Assert.Equal([errorTag, endTag], calls);
    }

    [Fact]
    public async Task EnsureSignalsFreshAsync_SkipsUnknownAndFalseSignals()
    {
        var subscription = TestInfrastructure.CreateSubscription();
        var values = TestInfrastructure.GetSubscriptionValues(subscription);
        var step = new PlcStepStub("DB_VI.Block_Test");
        var endTag = PlcBlockTagHelper.GetEndTag(step)!;
        values[endTag] = false;
        var calls = 0;

        await PlcRetrySignalFreshnessGuard.EnsureSignalsFreshAsync(
            step,
            subscription,
            (_, _, _) =>
            {
                calls++;
                return Task.CompletedTask;
            },
            TimeSpan.FromSeconds(1),
            (_, _, _) => { },
            "test retry",
            CancellationToken.None);

        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task EnsureSignalsFreshAsync_PropagatesCancellationDuringWait()
    {
        using var cts = new CancellationTokenSource();
        var subscription = TestInfrastructure.CreateSubscription();
        var values = TestInfrastructure.GetSubscriptionValues(subscription);
        var step = new PlcStepStub("DB_VI.Block_Test");
        values[PlcBlockTagHelper.GetErrorTag(step)!] = true;

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            PlcRetrySignalFreshnessGuard.EnsureSignalsFreshAsync(
                step,
                subscription,
                (_, _, token) => Task.Delay(10, token),
                TimeSpan.FromSeconds(1),
                (_, _, _) => { },
                "test retry",
                cts.Token));
    }

    private sealed class PlcStepStub(string plcBlockPath) : IHasPlcBlockPath
    {
        public string PlcBlockPath => plcBlockPath;
    }
}

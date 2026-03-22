using System.Reflection;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;
using Final_Test_Hybrid.Tests.TestSupport;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class StartTimer1StepTimingTests
{
    [Fact]
    public async Task ExecuteStartTimer1Async_AddsZeroDurationTimingRecord()
    {
        var context = PreExecutionTestContextFactory.Create();

        try
        {
            var preExecutionContext = CreateContext(context.Coordinator, "TEST-BARCODE");

            var result = await InvokeExecuteStartTimer1Async(
                context.Coordinator,
                preExecutionContext,
                CancellationToken.None);

            Assert.Equal(PreExecutionStatus.Continue, result.Status);

            var records = context.StepTimingService.GetAll();
            var record = Assert.Single(records);
            Assert.Equal("Misc/StartTimer1", record.Name);
            Assert.Equal("Запуск таймера 1", record.Description);
            Assert.Equal("00.00", record.Duration);
            Assert.False(record.IsRunning);
        }
        finally
        {
            context.BoilerState.StopChangeoverTimer();
            context.StepTimingService.Dispose();
        }
    }

    private static PreExecutionContext CreateContext(PreExecutionCoordinator coordinator, string barcode)
    {
        var method = typeof(PreExecutionCoordinator).GetMethod(
            "CreateContext",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        return Assert.IsType<PreExecutionContext>(method.Invoke(coordinator, [barcode]));
    }

    private static async Task<PreExecutionResult> InvokeExecuteStartTimer1Async(
        PreExecutionCoordinator coordinator,
        PreExecutionContext context,
        CancellationToken ct)
    {
        var method = typeof(PreExecutionCoordinator).GetMethod(
            "ExecuteStartTimer1Async",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task<PreExecutionResult>>(method.Invoke(coordinator, [context, ct]));
        return await task;
    }
}

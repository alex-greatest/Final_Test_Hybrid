using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;
using Final_Test_Hybrid.Tests.TestSupport;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class PreExecutionHardResetScannerTests
{
    [Fact]
    public async Task StartMainLoopAsync_RearmsBarcodeWait_AfterNonPlcHardReset()
    {
        using var loggerFactory = CreateLoggerFactory(out var logs);
        var context = PreExecutionTestContextFactory.Create(loggerFactory);
        using var loopCts = new CancellationTokenSource();

        try
        {
            var loopTask = context.Coordinator.StartMainLoopAsync(loopCts.Token);
            var firstWait = await WaitForBarcodeSourceAsync(context.Coordinator);

            TestInfrastructure.InvokePrivate(context.Coordinator, "HandleHardReset");
            await WaitUntilAsync(() => firstWait.Task.IsCanceled, TimeSpan.FromSeconds(2));

            var secondWait = await WaitForBarcodeSourceAsync(context.Coordinator, firstWait);

            Assert.NotSame(firstWait, secondWait);
            Assert.True(context.Coordinator.IsAcceptingInput);
            Assert.Contains(
                logs.Entries,
                entry => entry.Level == LogLevel.Debug
                    && entry.Message.Contains("non_plc_hard_reset_cancel_barcode_wait", StringComparison.Ordinal)
                    && entry.Message.Contains("barcodeWaitActive=True", StringComparison.Ordinal));

            await loopCts.CancelAsync();
            await loopTask;
        }
        finally
        {
            context.BoilerState.StopChangeoverTimer();
            context.StepTimingService.Dispose();
        }
    }

    private static ILoggerFactory CreateLoggerFactory(out RecordingLoggerProvider provider)
    {
        var recordingProvider = new RecordingLoggerProvider();
        provider = recordingProvider;
        return LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddProvider(recordingProvider);
        });
    }

    private static async Task<TaskCompletionSource<string>> WaitForBarcodeSourceAsync(
        PreExecutionCoordinator coordinator,
        TaskCompletionSource<string>? previous = null)
    {
        return await WaitUntilAsync(
            () =>
            {
                var source = GetBarcodeSource(coordinator);
                if (source == null || ReferenceEquals(source, previous) || source.Task.IsCompleted)
                {
                    return null;
                }

                return coordinator.IsAcceptingInput ? source : null;
            },
            TimeSpan.FromSeconds(2));
    }

    private static TaskCompletionSource<string>? GetBarcodeSource(PreExecutionCoordinator coordinator)
    {
        return TestInfrastructure.GetPrivateField<TaskCompletionSource<string>?>(coordinator, "_barcodeSource");
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException("Условие теста не выполнилось вовремя.");
    }

    private static async Task<T> WaitUntilAsync<T>(Func<T?> probe, TimeSpan timeout) where T : class
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var value = probe();
            if (value != null)
            {
                return value;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException("Условие теста не выполнилось вовремя.");
    }
}

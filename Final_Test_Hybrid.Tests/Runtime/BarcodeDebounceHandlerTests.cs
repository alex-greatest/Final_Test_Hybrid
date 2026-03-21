using System.Runtime.CompilerServices;
using Final_Test_Hybrid.Services.OpcUa.Connection;
using Final_Test_Hybrid.Services.Scanner;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Scanning;
using Final_Test_Hybrid.Tests.TestSupport;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class BarcodeDebounceHandlerTests
{
    [Fact]
    public async Task DispatchBarcodeAsync_LogsNotAcceptingInput_AndDoesNotSubmitBarcode()
    {
        using var loggerFactory = CreateLoggerFactory(out var logs);
        var context = PreExecutionTestContextFactory.Create(loggerFactory);
        var barcodeSource = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var connectionState = new OpcUaConnectionState(TestInfrastructure.CreateLogger<OpcUaConnectionState>());
        connectionState.SetConnected(true, "test");
        TestInfrastructure.SetPrivateField(context.Coordinator, "_barcodeSource", barcodeSource);
        TestInfrastructure.InvokePrivate(context.Coordinator, "SetAcceptingInput", false);
        var handler = CreateHandler(context.Coordinator, connectionState, loggerFactory);

        try
        {
            await TestInfrastructure.InvokePrivateAsync(handler, "DispatchBarcodeAsync", "12345678901");

            Assert.False(barcodeSource.Task.IsCompleted);
            Assert.Contains(
                logs.Entries,
                entry => entry.Level == LogLevel.Debug
                    && entry.Message.Contains("reason=not_accepting_input", StringComparison.Ordinal)
                    && entry.Message.Contains("isAcceptingInput=False", StringComparison.Ordinal)
                    && entry.Message.Contains("isConnected=True", StringComparison.Ordinal));
        }
        finally
        {
            context.StepTimingService.Dispose();
        }
    }

    [Fact]
    public async Task DispatchBarcodeAsync_LogsOpcDisconnected_AndDoesNotSubmitBarcode()
    {
        using var loggerFactory = CreateLoggerFactory(out var logs);
        var context = PreExecutionTestContextFactory.Create(loggerFactory);
        var barcodeSource = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var connectionState = new OpcUaConnectionState(TestInfrastructure.CreateLogger<OpcUaConnectionState>());
        connectionState.SetConnected(false, "test");
        TestInfrastructure.SetPrivateField(context.Coordinator, "_barcodeSource", barcodeSource);
        TestInfrastructure.InvokePrivate(context.Coordinator, "SetAcceptingInput", true);
        var handler = CreateHandler(context.Coordinator, connectionState, loggerFactory);

        try
        {
            await TestInfrastructure.InvokePrivateAsync(handler, "DispatchBarcodeAsync", "12345678901");

            Assert.False(barcodeSource.Task.IsCompleted);
            Assert.Contains(
                logs.Entries,
                entry => entry.Level == LogLevel.Debug
                    && entry.Message.Contains("reason=opc_disconnected", StringComparison.Ordinal)
                    && entry.Message.Contains("isAcceptingInput=True", StringComparison.Ordinal)
                    && entry.Message.Contains("isConnected=False", StringComparison.Ordinal));
        }
        finally
        {
            context.StepTimingService.Dispose();
        }
    }

    private static BarcodeDebounceHandler CreateHandler(
        PreExecutionCoordinator coordinator,
        OpcUaConnectionState connectionState,
        ILoggerFactory loggerFactory)
    {
        return new BarcodeDebounceHandler(
            new BarcodeScanService(),
            CreateUninitialized<StepStatusReporter>(),
            coordinator,
            connectionState,
            loggerFactory.CreateLogger<BarcodeDebounceHandler>());
    }

    private static T CreateUninitialized<T>() where T : class
    {
        return (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
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
}

using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Diagnostic.Models;
using Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Final_Test_Hybrid.Services.Steps.Steps.Coms;
using Final_Test_Hybrid.Tests.TestSupport;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class StandModeWriteExecutionHelperTests
{
    [Fact]
    public async Task ExecuteAsync_WritesImmediately_WhenDispatcherAlreadyReady()
    {
        var dispatcher = new TestModbusDispatcher
        {
            IsStarted = true,
            IsConnected = true,
            IsReconnecting = false,
            LastPingData = new DiagnosticPingData()
        };
        var writeCalls = 0;

        var result = await StandModeWriteExecutionHelper.ExecuteAsync(
            CreateContext(),
            dispatcher,
            ct =>
            {
                _ = ct;
                writeCalls++;
                return Task.FromResult(DiagnosticWriteResult.Ok(1000));
            },
            TestInfrastructure.CreateDualLogger<StandModeWriteExecutionHelperTests>(),
            TimeSpan.FromMilliseconds(200),
            TimeSpan.FromMilliseconds(20),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, writeCalls);
    }

    [Fact]
    public async Task ExecuteAsync_RetriesAfterReconnectRace_ThenWritesAfterReadyStateRestored()
    {
        var dispatcher = new TestModbusDispatcher
        {
            IsStarted = true,
            IsConnected = true,
            IsReconnecting = false,
            LastPingData = new DiagnosticPingData()
        };
        var writeCalls = 0;

        var task = StandModeWriteExecutionHelper.ExecuteAsync(
            CreateContext(),
            dispatcher,
            ct =>
            {
                _ = ct;
                writeCalls++;

                if (writeCalls != 1)
                {
                    return Task.FromResult(DiagnosticWriteResult.Ok(1000));
                }

                dispatcher.IsConnected = false;
                dispatcher.IsReconnecting = true;
                dispatcher.LastPingData = null;
                return Task.FromResult(CreateReconnectRejectedResult());
            },
            TestInfrastructure.CreateDualLogger<StandModeWriteExecutionHelperTests>(),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromMilliseconds(25),
            CancellationToken.None);

        await Task.Delay(120);
        dispatcher.IsConnected = true;
        dispatcher.IsReconnecting = false;
        dispatcher.LastPingData = new DiagnosticPingData();

        var result = await task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(result.Success);
        Assert.Equal(2, writeCalls);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotRetry_WhenWriteFailedForNonReconnectReason()
    {
        var dispatcher = new TestModbusDispatcher
        {
            IsStarted = true,
            IsConnected = true,
            IsReconnecting = false,
            LastPingData = new DiagnosticPingData()
        };
        var writeCalls = 0;
        const string error = "The operation has timed out.";

        var result = await StandModeWriteExecutionHelper.ExecuteAsync(
            CreateContext(),
            dispatcher,
            ct =>
            {
                _ = ct;
                writeCalls++;
                return Task.FromResult(DiagnosticWriteResult.Fail(1000, error, DiagnosticFailureKind.Communication));
            },
            TestInfrastructure.CreateDualLogger<StandModeWriteExecutionHelperTests>(),
            TimeSpan.FromMilliseconds(200),
            TimeSpan.FromMilliseconds(20),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(DiagnosticFailureKind.Communication, result.FailureKind);
        Assert.Equal(error, result.Error);
        Assert.Equal(1, writeCalls);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsCommunicationFail_WhenReadyStateWasNotRestoredInTime()
    {
        var dispatcher = new TestModbusDispatcher
        {
            IsStarted = true,
            IsConnected = false,
            IsReconnecting = true
        };
        var writeCalls = 0;

        var result = await StandModeWriteExecutionHelper.ExecuteAsync(
            CreateContext(),
            dispatcher,
            ct =>
            {
                _ = ct;
                writeCalls++;
                return Task.FromResult(DiagnosticWriteResult.Ok(1000));
            },
            TestInfrastructure.CreateDualLogger<StandModeWriteExecutionHelperTests>(),
            TimeSpan.FromMilliseconds(120),
            TimeSpan.FromMilliseconds(20),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(DiagnosticFailureKind.Communication, result.FailureKind);
        Assert.Contains("не восстановлена", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, writeCalls);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsCommunicationFail_WhenReconnectRaceConsumesWholeDeadline()
    {
        var dispatcher = new TestModbusDispatcher
        {
            IsStarted = true,
            IsConnected = true,
            IsReconnecting = false,
            LastPingData = new DiagnosticPingData()
        };
        var writeCalls = 0;

        var result = await StandModeWriteExecutionHelper.ExecuteAsync(
            CreateContext(),
            dispatcher,
            ct =>
            {
                _ = ct;
                writeCalls++;
                dispatcher.IsConnected = false;
                dispatcher.IsReconnecting = true;
                dispatcher.LastPingData = null;

                if (writeCalls == 1)
                {
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(40, CancellationToken.None);
                        dispatcher.IsConnected = true;
                        dispatcher.IsReconnecting = false;
                        dispatcher.LastPingData = new DiagnosticPingData();
                    }, CancellationToken.None);
                }

                return Task.FromResult(CreateReconnectRejectedResult());
            },
            TestInfrastructure.CreateDualLogger<StandModeWriteExecutionHelperTests>(),
            TimeSpan.FromMilliseconds(180),
            TimeSpan.FromMilliseconds(20),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(DiagnosticFailureKind.Communication, result.FailureKind);
        Assert.Contains("не восстановлена", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, writeCalls);
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsOperationCanceledException_WhenCanceledDuringWait()
    {
        var dispatcher = new TestModbusDispatcher
        {
            IsStarted = true,
            IsConnected = false,
            IsReconnecting = true
        };
        var writeCalls = 0;
        using var cts = new CancellationTokenSource();

        var task = StandModeWriteExecutionHelper.ExecuteAsync(
            CreateContext(),
            dispatcher,
            ct =>
            {
                _ = ct;
                writeCalls++;
                return Task.FromResult(DiagnosticWriteResult.Ok(1000));
            },
            TestInfrastructure.CreateDualLogger<StandModeWriteExecutionHelperTests>(),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromMilliseconds(25),
            cts.Token);

        cts.CancelAfter(80);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.Equal(0, writeCalls);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsCommunicationFail_WhenDispatcherAlreadyStopped()
    {
        var dispatcher = new TestModbusDispatcher
        {
            IsStarted = false
        };
        var writeCalls = 0;

        var result = await StandModeWriteExecutionHelper.ExecuteAsync(
            CreateContext(),
            dispatcher,
            ct =>
            {
                _ = ct;
                writeCalls++;
                return Task.FromResult(DiagnosticWriteResult.Ok(1000));
            },
            TestInfrastructure.CreateDualLogger<StandModeWriteExecutionHelperTests>(),
            TimeSpan.FromMilliseconds(200),
            TimeSpan.FromMilliseconds(20),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(DiagnosticFailureKind.Communication, result.FailureKind);
        Assert.Contains("остановлен", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, writeCalls);
    }

    private static TestStepContext CreateContext()
    {
        return new TestStepContext(
            columnIndex: 0,
            stepPacingWindow: TimeSpan.Zero,
            opcUa: null!,
            logger: TestInfrastructure.CreateLogger<TestStepContext>(),
            recipeProvider: null!,
            pauseToken: new PauseTokenSource(),
            diagReader: null!,
            diagWriter: null!,
            tagWaiter: null!,
            rangeSliderUiState: null!);
    }

    private static DiagnosticWriteResult CreateReconnectRejectedResult()
    {
        return DiagnosticWriteResult.Fail(
            1000,
            "Команда прервана: начато переподключение Modbus до начала выполнения. State=pending",
            DiagnosticFailureKind.Communication);
    }

    private sealed class TestModbusDispatcher : IModbusDispatcher
    {
        public bool IsConnected { get; set; }
        public bool IsReconnecting { get; set; }
        public bool IsStarted { get; set; }
        public DiagnosticPingData? LastPingData { get; set; }

        public event Func<Task>? Disconnecting
        {
            add { }
            remove { }
        }

        public event Action? Connected
        {
            add { }
            remove { }
        }

        public event Action? Stopped
        {
            add { }
            remove { }
        }

        public event Action<DiagnosticPingData>? PingDataUpdated
        {
            add { }
            remove { }
        }

        public ValueTask EnqueueAsync(IModbusCommand command, CancellationToken ct = default)
        {
            return ValueTask.CompletedTask;
        }

        public Task StartAsync(CancellationToken ct = default)
        {
            IsStarted = true;
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            IsStarted = false;
            IsConnected = false;
            IsReconnecting = false;
            LastPingData = null;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}

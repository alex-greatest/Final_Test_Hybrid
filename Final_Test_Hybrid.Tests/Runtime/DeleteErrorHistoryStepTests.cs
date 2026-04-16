using System.IO;
using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Final_Test_Hybrid.Services.Diagnostic.Models;
using Final_Test_Hybrid.Services.Diagnostic.Protocol;
using Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Final_Test_Hybrid.Services.Steps.Steps.Coms;
using Final_Test_Hybrid.Tests.TestSupport;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class DeleteErrorHistoryStepTests
{
    [Fact]
    public async Task ExecuteAsync_Passes_WhenFirstWriteSucceeded()
    {
        var dispatcher = CreateReadyDispatcher();
        var client = new ScriptedWriteModbusClient();
        var step = CreateStep(dispatcher, TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(20));

        var result = await step.ExecuteAsync(CreateContext(client), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(new ushort[] { 1153 }, client.Addresses);
        Assert.Equal(new ushort[] { 0 }, client.Values);
    }

    [Fact]
    public async Task ExecuteAsync_RetriesOnce_WhenInitialWriteFailedByReconnectRace()
    {
        var dispatcher = CreateReadyDispatcher();
        var client = new ScriptedWriteModbusClient();
        var step = CreateStep(dispatcher, TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(25));

        client.EnqueueWrite((_, _, _) =>
        {
            dispatcher.IsConnected = false;
            dispatcher.IsReconnecting = true;
            dispatcher.LastPingData = null;
            throw new IOException("Команда прервана: начато переподключение Modbus до начала выполнения. State=pending");
        });

        var execution = step.ExecuteAsync(CreateContext(client), CancellationToken.None);

        await client.FirstWriteObserved.WaitAsync(TimeSpan.FromSeconds(1));
        dispatcher.IsConnected = true;
        dispatcher.IsReconnecting = false;
        dispatcher.LastPingData = new DiagnosticPingData();

        var result = await execution.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(result.Success);
        Assert.Equal(2, client.WriteSingleRegisterCalls);
        Assert.Equal(new ushort[] { 1153, 1153 }, client.Addresses);
        Assert.Equal(new ushort[] { 0, 0 }, client.Values);
    }

    [Fact]
    public async Task ExecuteAsync_RetriesOnce_WhenInitialWriteWasRejectedDuringReconnect()
    {
        var dispatcher = CreateReadyDispatcher();
        var client = new ScriptedWriteModbusClient();
        var step = CreateStep(dispatcher, TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(25));

        client.EnqueueWrite((_, _, _) =>
        {
            dispatcher.IsConnected = false;
            dispatcher.IsReconnecting = true;
            dispatcher.LastPingData = null;
            throw new IOException("Команда прервана: начато переподключение Modbus до начала выполнения. State=rejected");
        });

        var execution = step.ExecuteAsync(CreateContext(client), CancellationToken.None);

        await client.FirstWriteObserved.WaitAsync(TimeSpan.FromSeconds(1));
        dispatcher.IsConnected = true;
        dispatcher.IsReconnecting = false;
        dispatcher.LastPingData = new DiagnosticPingData();

        var result = await execution.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(result.Success);
        Assert.Equal(2, client.WriteSingleRegisterCalls);
        Assert.Equal(new ushort[] { 1153, 1153 }, client.Addresses);
        Assert.Equal(new ushort[] { 0, 0 }, client.Values);
    }

    [Fact]
    public async Task ExecuteAsync_Fails_WhenReadyStateWasNotRestoredInTime()
    {
        var dispatcher = CreateReadyDispatcher();
        var client = new ScriptedWriteModbusClient();
        var step = CreateStep(dispatcher, TimeSpan.FromMilliseconds(120), TimeSpan.FromMilliseconds(20));

        client.EnqueueWrite((_, _, _) =>
        {
            dispatcher.IsConnected = false;
            dispatcher.IsReconnecting = true;
            dispatcher.LastPingData = null;
            throw new IOException("Команда прервана: начато переподключение Modbus до начала выполнения. State=pending");
        });

        var result = await step.ExecuteAsync(CreateContext(client), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("не восстановлена", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, client.WriteSingleRegisterCalls);
    }

    [Fact]
    public async Task ExecuteAsync_FailsFast_WhenDispatcherStoppedBeforeRetry()
    {
        var dispatcher = CreateReadyDispatcher();
        var client = new ScriptedWriteModbusClient();
        var step = CreateStep(dispatcher, TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(25));

        client.EnqueueWrite((_, _, _) =>
        {
            dispatcher.IsStarted = false;
            dispatcher.IsConnected = false;
            dispatcher.IsReconnecting = false;
            dispatcher.LastPingData = null;
            throw new IOException("Команда прервана: начато переподключение Modbus до начала выполнения. State=pending");
        });

        var result = await step.ExecuteAsync(CreateContext(client), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(1, client.WriteSingleRegisterCalls);
        Assert.Contains("ModbusDispatcher остановлен", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_Fails_WhenRetryWriteStillFailedAfterReadyStateRestored()
    {
        var dispatcher = CreateReadyDispatcher();
        var client = new ScriptedWriteModbusClient();
        var step = CreateStep(dispatcher, TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(25));

        client.EnqueueWrite((_, _, _) =>
        {
            dispatcher.IsConnected = false;
            dispatcher.IsReconnecting = true;
            dispatcher.LastPingData = null;
            throw new IOException("Команда прервана: начато переподключение Modbus до начала выполнения. State=pending");
        });
        client.EnqueueWrite((_, _, _) =>
        {
            throw new InvalidOperationException("Функциональная ошибка записи");
        });

        var execution = step.ExecuteAsync(CreateContext(client), CancellationToken.None);

        await client.FirstWriteObserved.WaitAsync(TimeSpan.FromSeconds(1));
        dispatcher.IsConnected = true;
        dispatcher.IsReconnecting = false;
        dispatcher.LastPingData = new DiagnosticPingData();

        var result = await execution.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.False(result.Success);
        Assert.Contains("Функциональная ошибка записи", result.Message, StringComparison.Ordinal);
        Assert.Equal(2, client.WriteSingleRegisterCalls);
    }

    [Fact]
    public async Task ExecuteAsync_Cancels_WhenTokenCanceledWhileWaitingForDispatcherReady()
    {
        var dispatcher = CreateReadyDispatcher();
        var client = new ScriptedWriteModbusClient();
        var step = CreateStep(dispatcher, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(25));

        client.EnqueueWrite((_, _, _) =>
        {
            dispatcher.IsConnected = false;
            dispatcher.IsReconnecting = true;
            dispatcher.LastPingData = null;
            throw new IOException("Команда прервана: начато переподключение Modbus до начала выполнения. State=pending");
        });

        using var cts = new CancellationTokenSource();
        var execution = step.ExecuteAsync(CreateContext(client), cts.Token);

        await client.FirstWriteObserved.WaitAsync(TimeSpan.FromSeconds(1));
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await execution.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Single(client.CancellationTokens);
        Assert.Equal(cts.Token, client.CancellationTokens[0]);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotRetry_WhenCommunicationFailureIsNotReconnectRace()
    {
        var dispatcher = CreateReadyDispatcher();
        var client = new ScriptedWriteModbusClient();
        var step = CreateStep(dispatcher, TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(25));

        client.EnqueueWrite((_, _, _) =>
        {
            throw new IOException("Таймаут записи без переподключения");
        });

        var result = await step.ExecuteAsync(CreateContext(client), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(1, client.WriteSingleRegisterCalls);
        Assert.Contains("Таймаут записи без переподключения", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotRetry_WhenFirstWriteFailedFunctionally()
    {
        var dispatcher = CreateReadyDispatcher();
        var client = new ScriptedWriteModbusClient();
        var step = CreateStep(dispatcher, TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(25));

        client.EnqueueWrite((_, _, _) =>
        {
            throw new InvalidOperationException("Функциональная ошибка первой записи");
        });

        var result = await step.ExecuteAsync(CreateContext(client), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(1, client.WriteSingleRegisterCalls);
        Assert.Contains("Функциональная ошибка первой записи", result.Message, StringComparison.Ordinal);
    }

    private static DeleteErrorHistoryStep CreateStep(
        TestModbusDispatcher dispatcher,
        TimeSpan readyWaitTimeout,
        TimeSpan pollInterval)
    {
        return new DeleteErrorHistoryStep(
            new DiagnosticSettings { BaseAddressOffset = 1 },
            dispatcher,
            TestInfrastructure.CreateDualLogger<DeleteErrorHistoryStep>(),
            readyWaitTimeout,
            pollInterval);
    }

    private static TestStepContext CreateContext(ScriptedWriteModbusClient client)
    {
        var pauseToken = new Final_Test_Hybrid.Services.Common.PauseTokenSource();
        var reader = new RegisterReader(client, TestInfrastructure.CreateLogger<RegisterReader>(), new TestStepLoggerStub());
        var writer = new RegisterWriter(client, TestInfrastructure.CreateLogger<RegisterWriter>(), new TestStepLoggerStub());

        return new TestStepContext(
            columnIndex: 0,
            stepPacingWindow: TimeSpan.Zero,
            opcUa: null!,
            logger: TestInfrastructure.CreateLogger<TestStepContext>(),
            recipeProvider: null!,
            pauseToken: pauseToken,
            diagReader: new PausableRegisterReader(reader, pauseToken),
            diagWriter: new PausableRegisterWriter(writer, pauseToken),
            tagWaiter: null!,
            rangeSliderUiState: null!);
    }

    private static TestModbusDispatcher CreateReadyDispatcher()
    {
        return new TestModbusDispatcher
        {
            IsStarted = true,
            IsConnected = true,
            IsReconnecting = false,
            LastPingData = new DiagnosticPingData()
        };
    }

    private sealed class ScriptedWriteModbusClient : IModbusClient
    {
        private readonly Queue<Action<ushort, ushort, CancellationToken>> _writeActions = [];
        private readonly TaskCompletionSource _firstWriteObserved =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int WriteSingleRegisterCalls { get; private set; }
        public List<ushort> Addresses { get; } = [];
        public List<ushort> Values { get; } = [];
        public List<CancellationToken> CancellationTokens { get; } = [];
        public Task FirstWriteObserved => _firstWriteObserved.Task;

        public void EnqueueWrite(Action<ushort, ushort, CancellationToken> action)
        {
            _writeActions.Enqueue(action);
        }

        public Task<ushort[]> ReadHoldingRegistersAsync(
            ushort address,
            ushort count,
            CommandPriority priority = CommandPriority.High,
            CancellationToken ct = default)
        {
            _ = address;
            _ = count;
            _ = priority;
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(new ushort[count]);
        }

        public Task WriteSingleRegisterAsync(
            ushort address,
            ushort value,
            CommandPriority priority = CommandPriority.High,
            CancellationToken ct = default)
        {
            _ = priority;
            ct.ThrowIfCancellationRequested();
            WriteSingleRegisterCalls++;
            Addresses.Add(address);
            Values.Add(value);
            CancellationTokens.Add(ct);
            _firstWriteObserved.TrySetResult();

            if (_writeActions.Count > 0)
            {
                _writeActions.Dequeue()(address, value, ct);
            }

            return Task.CompletedTask;
        }

        public Task WriteMultipleRegistersAsync(
            ushort address,
            ushort[] values,
            CommandPriority priority = CommandPriority.High,
            CancellationToken ct = default)
        {
            _ = address;
            _ = values;
            _ = priority;
            ct.ThrowIfCancellationRequested();
            throw new NotSupportedException();
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
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
            _ = command;
            _ = ct;
            return ValueTask.CompletedTask;
        }

        public Task StartAsync(CancellationToken ct = default)
        {
            _ = ct;
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

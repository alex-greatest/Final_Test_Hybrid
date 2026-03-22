using Final_Test_Hybrid.Services.Main.Messages;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class MessageServiceResolverTests
{
    [Fact]
    public void CompletionActive_ReturnsCompletionMessage()
    {
        var snapshot = CreateSnapshot() with { IsCompletionActive = true };

        var message = MessageServiceResolver.Resolve(snapshot);

        Assert.Equal("Тест завершён. Ожидание решения PLC...", message);
    }

    [Fact]
    public void CompletionActiveWithDisconnectedRawState_ReturnsCompletionMessage()
    {
        var snapshot = CreateSnapshot() with
        {
            IsConnected = false,
            IsCompletionActive = true
        };

        var message = MessageServiceResolver.Resolve(snapshot);

        Assert.Equal("Тест завершён. Ожидание решения PLC...", message);
    }

    [Fact]
    public void PostAskEndWithAutoReadyOff_ReturnsPostAskEndMessage()
    {
        var snapshot = CreateSnapshot() with
        {
            IsAutoReady = false,
            IsResetUiBusy = true,
            IsPostAskEndActive = true
        };

        var message = MessageServiceResolver.Resolve(snapshot);

        Assert.Equal("Сброс подтверждён. Ожидание решения PLC...", message);
    }

    [Fact]
    public void PostAskEndWithoutResetBusy_UsesTerminalOwnerMessage()
    {
        var snapshot = CreateSnapshot() with
        {
            IsAutoReady = false,
            IsTestRunning = false,
            IsResetUiBusy = false,
            IsPostAskEndActive = true
        };

        var message = MessageServiceResolver.Resolve(snapshot);

        Assert.Equal("Сброс подтверждён. Ожидание решения PLC...", message);
    }

    [Fact]
    public void PlcConnectionLostPendingReset_BeatsRecoveredConnectionState()
    {
        var snapshot = CreateSnapshot() with
        {
            IsConnected = true,
            CurrentInterrupt = InterruptReason.PlcConnectionLost
        };

        var message = MessageServiceResolver.Resolve(snapshot);

        Assert.Equal("Потеря связи с PLC. Ожидание сброса...", message);
    }

    [Fact]
    public void DisconnectedIdle_ReturnsDisconnectedMessage()
    {
        var snapshot = CreateSnapshot() with
        {
            IsConnected = false,
            IsTestRunning = false
        };

        var message = MessageServiceResolver.Resolve(snapshot);

        Assert.Equal("Нет связи с PLC", message);
    }

    [Fact]
    public void AutoModeDisabledInterrupt_ReturnsAutoMessage()
    {
        var snapshot = CreateSnapshot() with
        {
            CurrentInterrupt = InterruptReason.AutoModeDisabled
        };

        var message = MessageServiceResolver.Resolve(snapshot);

        Assert.Equal("Ожидание автомата", message);
    }

    [Fact]
    public void IdleAuthenticatedWithoutAutoReady_UsesFallbackWaitAutoMessage()
    {
        var snapshot = CreateSnapshot() with
        {
            IsAutoReady = false,
            IsTestRunning = false
        };

        var message = MessageServiceResolver.Resolve(snapshot);

        Assert.Equal("Ожидание автомата", message);
    }

    [Fact]
    public void BoilerLockInterrupt_KeepsBoilerLockMessage()
    {
        var snapshot = CreateSnapshot() with
        {
            CurrentInterrupt = InterruptReason.BoilerLock
        };

        var message = MessageServiceResolver.Resolve(snapshot);

        Assert.Equal("Блокировка котла. Ожидание восстановления", message);
    }

    [Fact]
    public void TagTimeoutResetting_KeepsResetTimeoutMessage()
    {
        var snapshot = CreateSnapshot() with
        {
            CurrentInterrupt = InterruptReason.TagTimeout,
            IsResetUiBusy = true
        };

        var message = MessageServiceResolver.Resolve(snapshot);

        Assert.Equal("Нет ответа от ПЛК. Выполняется сброс...", message);
    }

    [Fact]
    public void GasValveTubeMessageDuringRuntime_ReturnsLowPriorityMessage()
    {
        var snapshot = CreateSnapshot() with
        {
            IsGasValveTubeMessageActive = true
        };

        var message = MessageServiceResolver.Resolve(snapshot);

        Assert.Equal("Не подключена трубка газового клапана", message);
    }

    [Fact]
    public void EarthClipMessageDuringRuntime_ReturnsLowPriorityMessage()
    {
        var snapshot = CreateSnapshot() with
        {
            IsEarthClipMessageActive = true
        };

        var message = MessageServiceResolver.Resolve(snapshot);

        Assert.Equal("Подключите клипсу заземления", message);
    }

    [Fact]
    public void PowerCableMessageDuringRuntime_ReturnsLowPriorityMessage()
    {
        var snapshot = CreateSnapshot() with
        {
            IsPowerCableMessageActive = true
        };

        var message = MessageServiceResolver.Resolve(snapshot);

        Assert.Equal("Подключите силовой кабель", message);
    }

    [Fact]
    public void Disconnected_BeatsGasValveTubeMessage()
    {
        var snapshot = CreateSnapshot() with
        {
            IsConnected = false,
            IsGasValveTubeMessageActive = true
        };

        var message = MessageServiceResolver.Resolve(snapshot);

        Assert.Equal("Нет связи с PLC", message);
    }

    [Fact]
    public void Disconnected_BeatsPowerCableMessage()
    {
        var snapshot = CreateSnapshot() with
        {
            IsConnected = false,
            IsPowerCableMessageActive = true
        };

        var message = MessageServiceResolver.Resolve(snapshot);

        Assert.Equal("Нет связи с PLC", message);
    }

    [Fact]
    public void GenericReset_BeatsEarthClipMessage()
    {
        var snapshot = CreateSnapshot() with
        {
            IsResetUiBusy = true,
            IsEarthClipMessageActive = true
        };

        var message = MessageServiceResolver.Resolve(snapshot);

        Assert.Equal("Сброс теста...", message);
    }

    [Fact]
    public void GenericReset_BeatsPowerCableMessage()
    {
        var snapshot = CreateSnapshot() with
        {
            IsResetUiBusy = true,
            IsPowerCableMessageActive = true
        };

        var message = MessageServiceResolver.Resolve(snapshot);

        Assert.Equal("Сброс теста...", message);
    }

    [Fact]
    public void CompletionActive_BeatsEarthClipMessage()
    {
        var snapshot = CreateSnapshot() with
        {
            IsCompletionActive = true,
            IsEarthClipMessageActive = true
        };

        var message = MessageServiceResolver.Resolve(snapshot);

        Assert.Equal("Тест завершён. Ожидание решения PLC...", message);
    }

    [Fact]
    public void CompletionActive_BeatsPowerCableMessage()
    {
        var snapshot = CreateSnapshot() with
        {
            IsCompletionActive = true,
            IsPowerCableMessageActive = true
        };

        var message = MessageServiceResolver.Resolve(snapshot);

        Assert.Equal("Тест завершён. Ожидание решения PLC...", message);
    }

    [Fact]
    public void PostAskEndActive_BeatsEarthClipMessage()
    {
        var snapshot = CreateSnapshot() with
        {
            IsPostAskEndActive = true,
            IsEarthClipMessageActive = true
        };

        var message = MessageServiceResolver.Resolve(snapshot);

        Assert.Equal("Сброс подтверждён. Ожидание решения PLC...", message);
    }

    [Fact]
    public void PostAskEndActive_BeatsPowerCableMessage()
    {
        var snapshot = CreateSnapshot() with
        {
            IsPostAskEndActive = true,
            IsPowerCableMessageActive = true
        };

        var message = MessageServiceResolver.Resolve(snapshot);

        Assert.Equal("Сброс подтверждён. Ожидание решения PLC...", message);
    }

    [Fact]
    public void PlcConnectionLost_BeatsEarthClipMessage()
    {
        var snapshot = CreateSnapshot() with
        {
            CurrentInterrupt = InterruptReason.PlcConnectionLost,
            IsEarthClipMessageActive = true
        };

        var message = MessageServiceResolver.Resolve(snapshot);

        Assert.Equal("Потеря связи с PLC. Ожидание сброса...", message);
    }

    [Fact]
    public void PlcConnectionLost_BeatsPowerCableMessage()
    {
        var snapshot = CreateSnapshot() with
        {
            CurrentInterrupt = InterruptReason.PlcConnectionLost,
            IsPowerCableMessageActive = true
        };

        var message = MessageServiceResolver.Resolve(snapshot);

        Assert.Equal("Потеря связи с PLC. Ожидание сброса...", message);
    }

    [Fact]
    public void TagTimeout_BeatsEarthClipMessage()
    {
        var snapshot = CreateSnapshot() with
        {
            CurrentInterrupt = InterruptReason.TagTimeout,
            IsEarthClipMessageActive = true
        };

        var message = MessageServiceResolver.Resolve(snapshot);

        Assert.Equal("Нет ответа от ПЛК", message);
    }

    [Fact]
    public void TagTimeout_BeatsPowerCableMessage()
    {
        var snapshot = CreateSnapshot() with
        {
            CurrentInterrupt = InterruptReason.TagTimeout,
            IsPowerCableMessageActive = true
        };

        var message = MessageServiceResolver.Resolve(snapshot);

        Assert.Equal("Нет ответа от ПЛК", message);
    }

    private static MessageSnapshot CreateSnapshot()
    {
        return new MessageSnapshot(
            IsAuthenticated: true,
            IsAutoReady: true,
            IsConnected: true,
            IsScanModeEnabled: false,
            IsTestRunning: true,
            Phase: null,
            CurrentInterrupt: null,
            IsResetUiBusy: false,
            IsCompletionActive: false,
            IsPostAskEndActive: false,
            IsGasValveTubeMessageActive: false,
            IsEarthClipMessageActive: false,
            IsPowerCableMessageActive: false);
    }
}

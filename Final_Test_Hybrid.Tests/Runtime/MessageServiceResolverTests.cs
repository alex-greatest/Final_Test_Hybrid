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
            IsPostAskEndActive: false);
    }
}

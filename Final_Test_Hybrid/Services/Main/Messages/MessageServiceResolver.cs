namespace Final_Test_Hybrid.Services.Main.Messages;

using Steps.Infrastructure.Execution.ErrorCoordinator;

internal readonly record struct MessageSnapshot(
    bool IsAuthenticated,
    bool IsAutoReady,
    bool IsConnected,
    bool IsScanModeEnabled,
    bool IsTestRunning,
    ExecutionPhase? Phase,
    InterruptReason? CurrentInterrupt,
    bool IsResetUiBusy,
    bool IsCompletionActive,
    bool IsPostAskEndActive,
    bool IsGasValveTubeMessageActive,
    bool IsEarthClipMessageActive,
    bool IsPowerCableMessageActive);

internal enum MessageScenario
{
    None,
    PlcConnectionLostResetting,
    TagTimeoutResetting,
    PlcConnectionLostPendingReset,
    TagTimeout,
    BoilerLock,
    Disconnected,
    CompletionActive,
    PostAskEndActive,
    GenericReset,
    AutoModeDisabled,
    LoginRequired,
    WaitForAuto,
    ScanPrompt,
    ExecutionPhase,
    GasValveTubeNotConnected,
    EarthClipNotConnected,
    PowerCableNotConnected
}

internal static class MessageServiceResolver
{
    internal static string Resolve(MessageSnapshot snapshot)
    {
        return ResolveScenario(snapshot) switch
        {
            MessageScenario.PlcConnectionLostResetting => MessageTextResources.PlcConnectionLostResetting,
            MessageScenario.TagTimeoutResetting => MessageTextResources.TagTimeoutResetting,
            MessageScenario.PlcConnectionLostPendingReset => MessageTextResources.PlcConnectionLostPendingReset,
            MessageScenario.TagTimeout => MessageTextResources.TagTimeout,
            MessageScenario.BoilerLock => MessageTextResources.BoilerLock,
            MessageScenario.Disconnected => MessageTextResources.Disconnected,
            MessageScenario.CompletionActive => MessageTextResources.CompletionActive,
            MessageScenario.PostAskEndActive => MessageTextResources.PostAskEndActive,
            MessageScenario.GenericReset => MessageTextResources.GenericReset,
            MessageScenario.AutoModeDisabled => MessageTextResources.WaitForAuto,
            MessageScenario.LoginRequired => MessageTextResources.LoginRequired,
            MessageScenario.WaitForAuto => MessageTextResources.WaitForAuto,
            MessageScenario.ScanPrompt => MessageTextResources.ScanPrompt,
            MessageScenario.ExecutionPhase => GetPhaseMessage(snapshot.Phase),
            MessageScenario.GasValveTubeNotConnected => MessageTextResources.GasValveTubeNotConnected,
            MessageScenario.EarthClipNotConnected => MessageTextResources.EarthClipNotConnected,
            MessageScenario.PowerCableNotConnected => MessageTextResources.PowerCableNotConnected,
            _ => ""
        };
    }

    private static MessageScenario ResolveScenario(MessageSnapshot snapshot)
    {
        if (snapshot.CurrentInterrupt == InterruptReason.PlcConnectionLost && snapshot.IsResetUiBusy)
        {
            return MessageScenario.PlcConnectionLostResetting;
        }

        if (snapshot.CurrentInterrupt == InterruptReason.TagTimeout && snapshot.IsResetUiBusy)
        {
            return MessageScenario.TagTimeoutResetting;
        }

        if (!snapshot.IsConnected && snapshot.IsResetUiBusy)
        {
            return MessageScenario.PlcConnectionLostResetting;
        }

        if (snapshot.CurrentInterrupt == InterruptReason.PlcConnectionLost)
        {
            return MessageScenario.PlcConnectionLostPendingReset;
        }

        if (snapshot.CurrentInterrupt == InterruptReason.TagTimeout)
        {
            return MessageScenario.TagTimeout;
        }

        if (snapshot.CurrentInterrupt == InterruptReason.BoilerLock)
        {
            return MessageScenario.BoilerLock;
        }

        if (snapshot.IsCompletionActive)
        {
            return MessageScenario.CompletionActive;
        }

        if (snapshot.IsPostAskEndActive)
        {
            return MessageScenario.PostAskEndActive;
        }

        if (snapshot.IsResetUiBusy)
        {
            return MessageScenario.GenericReset;
        }

        if (!snapshot.IsConnected)
        {
            return MessageScenario.Disconnected;
        }

        if (snapshot.CurrentInterrupt == InterruptReason.AutoModeDisabled)
        {
            return MessageScenario.AutoModeDisabled;
        }

        if (!snapshot.IsAuthenticated)
        {
            return MessageScenario.LoginRequired;
        }

        if (IsIdleWaitForAuto(snapshot))
        {
            return MessageScenario.WaitForAuto;
        }

        if (snapshot.IsScanModeEnabled && !snapshot.IsTestRunning && snapshot.Phase == null)
        {
            return MessageScenario.ScanPrompt;
        }

        if (snapshot.Phase != null)
        {
            return MessageScenario.ExecutionPhase;
        }

        if (snapshot.IsGasValveTubeMessageActive && snapshot.IsTestRunning)
        {
            return MessageScenario.GasValveTubeNotConnected;
        }

        if (snapshot.IsEarthClipMessageActive && snapshot.IsTestRunning)
        {
            return MessageScenario.EarthClipNotConnected;
        }

        if (snapshot.IsPowerCableMessageActive && snapshot.IsTestRunning)
        {
            return MessageScenario.PowerCableNotConnected;
        }

        return MessageScenario.None;
    }

    private static bool IsIdleWaitForAuto(MessageSnapshot snapshot)
    {
        return snapshot.IsAuthenticated
            && !snapshot.IsAutoReady
            && !snapshot.IsTestRunning
            && snapshot.Phase == null
            && snapshot.CurrentInterrupt == null
            && !snapshot.IsResetUiBusy
            && !snapshot.IsCompletionActive
            && !snapshot.IsPostAskEndActive;
    }

    private static string GetPhaseMessage(ExecutionPhase? phase)
    {
        return phase switch
        {
            ExecutionPhase.BarcodeReceived => MessageTextResources.PhaseBarcodeReceived,
            ExecutionPhase.ValidatingSteps => MessageTextResources.PhaseValidatingSteps,
            ExecutionPhase.ValidatingRecipes => MessageTextResources.PhaseValidatingRecipes,
            ExecutionPhase.LoadingRecipes => MessageTextResources.PhaseLoadingRecipes,
            ExecutionPhase.CreatingDbRecords => MessageTextResources.PhaseCreatingDbRecords,
            ExecutionPhase.WaitingForAdapter => MessageTextResources.PhaseWaitingForAdapter,
            ExecutionPhase.WaitingForDiagnosticConnection => MessageTextResources.PhaseWaitingForDiagnosticConnection,
            _ => ""
        };
    }
}

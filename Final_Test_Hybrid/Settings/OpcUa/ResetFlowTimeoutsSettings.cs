namespace Final_Test_Hybrid.Settings.OpcUa;

public class ResetFlowTimeoutsSettings
{
    public int AskEndTimeoutSec { get; set; } = 60;
    public int ReconnectWaitTimeoutSec { get; set; } = 15;
    public int ResetHardTimeoutSec { get; set; } = 60;

    public void Validate()
    {
        ValidateAskEndTimeout();
        ValidateReconnectWaitTimeout();
        ValidateResetHardTimeout();
        ValidateHardTimeoutAgainstAskEnd();
        ValidateHardTimeoutAgainstReconnectWait();
    }

    private void ValidateAskEndTimeout()
    {
        if (AskEndTimeoutSec is < 5 or > 300)
        {
            throw new InvalidOperationException(
                $"OpcUa:ResetFlowTimeouts:AskEndTimeoutSec должен быть 5-300 сек (получено: {AskEndTimeoutSec})");
        }
    }

    private void ValidateReconnectWaitTimeout()
    {
        if (ReconnectWaitTimeoutSec is < 1 or > 120)
        {
            throw new InvalidOperationException(
                $"OpcUa:ResetFlowTimeouts:ReconnectWaitTimeoutSec должен быть 1-120 сек (получено: {ReconnectWaitTimeoutSec})");
        }
    }

    private void ValidateResetHardTimeout()
    {
        if (ResetHardTimeoutSec is < 5 or > 300)
        {
            throw new InvalidOperationException(
                $"OpcUa:ResetFlowTimeouts:ResetHardTimeoutSec должен быть 5-300 сек (получено: {ResetHardTimeoutSec})");
        }
    }

    private void ValidateHardTimeoutAgainstAskEnd()
    {
        if (ResetHardTimeoutSec < AskEndTimeoutSec)
        {
            throw new InvalidOperationException(
                "OpcUa:ResetFlowTimeouts:ResetHardTimeoutSec должен быть >= AskEndTimeoutSec");
        }
    }

    private void ValidateHardTimeoutAgainstReconnectWait()
    {
        if (ResetHardTimeoutSec < ReconnectWaitTimeoutSec)
        {
            throw new InvalidOperationException(
                "OpcUa:ResetFlowTimeouts:ResetHardTimeoutSec должен быть >= ReconnectWaitTimeoutSec");
        }
    }
}

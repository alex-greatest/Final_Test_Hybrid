namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;

public enum PreExecutionResolution
{
    Retry,
    Skip,
    SoftStop,
    HardReset,
    Timeout
}

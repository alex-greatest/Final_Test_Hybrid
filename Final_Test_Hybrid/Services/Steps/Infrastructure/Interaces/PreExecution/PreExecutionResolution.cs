namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.PreExecution;

public enum PreExecutionResolution
{
    Retry,
    Skip,
    SoftStop,
    HardReset,
    Timeout
}

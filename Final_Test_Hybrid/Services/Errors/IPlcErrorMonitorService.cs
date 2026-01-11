namespace Final_Test_Hybrid.Services.Errors;

public interface IPlcErrorMonitorService
{
    Task StartMonitoringAsync(CancellationToken ct = default);
}

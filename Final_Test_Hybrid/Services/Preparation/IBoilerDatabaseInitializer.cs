namespace Final_Test_Hybrid.Services.Preparation;

public record BoilerInitResult(bool IsSuccess, string? Error, long? BoilerId);

public interface IBoilerDatabaseInitializer
{
    Task<BoilerInitResult> InitializeAsync(
        string serialNumber,
        long boilerTypeCycleId,
        string operatorName,
        int shiftNumber);
}

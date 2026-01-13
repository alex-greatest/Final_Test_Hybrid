using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Database;

namespace Final_Test_Hybrid.Services.Preparation;

public class BoilerDatabaseInitializer(
    BoilerService boilerService,
    OperationService operationService,
    DualLogger<BoilerDatabaseInitializer> logger) : IBoilerDatabaseInitializer
{
    public async Task<BoilerInitResult> InitializeAsync(
        string serialNumber,
        long boilerTypeCycleId,
        string operatorName,
        int shiftNumber)
    {
        var boiler = await boilerService.FindOrCreateAsync(serialNumber, boilerTypeCycleId, operatorName);
        await operationService.CreateAsync(boiler.Id, operatorName, shiftNumber);

        logger.LogInformation("Записи в БД созданы: Boiler={BoilerId}", boiler.Id);

        return new BoilerInitResult(true, null, boiler.Id);
    }
}

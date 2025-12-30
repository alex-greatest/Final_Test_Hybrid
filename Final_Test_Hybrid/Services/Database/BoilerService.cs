using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Database.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Database;

public class BoilerService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<BoilerService> logger,
    IDatabaseLogger dbLogger)
{
    public async Task<Boiler> FindOrCreateAsync(string serialNumber, long boilerTypeCycleId, string operatorName)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        try
        {
            var existing = await dbContext.Boilers
                .FirstOrDefaultAsync(x => x.SerialNumber == serialNumber);
            if (existing != null)
            {
                return await UpdateExistingAsync(dbContext, existing, operatorName);
            }
            return await CreateNewAsync(dbContext, serialNumber, boilerTypeCycleId, operatorName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to find or create Boiler {SerialNumber}", serialNumber);
            dbLogger.LogError(ex, "Ошибка поиска/создания котла {SerialNumber}", serialNumber);
            throw new InvalidOperationException(DbConstraintErrorHandler.GetUserFriendlyMessage(ex), ex);
        }
    }

    private async Task<Boiler> UpdateExistingAsync(AppDbContext dbContext, Boiler existing, string operatorName)
    {
        existing.DateUpdate = DateTime.Now;
        existing.Status = OperationResultStatus.InWork;
        existing.Operator = operatorName;
        await dbContext.SaveChangesAsync();
        logger.LogInformation("Updated Boiler {Id}, SerialNumber: {SerialNumber}", existing.Id, existing.SerialNumber);
        dbLogger.LogInformation("Обновлён котёл {Id}, серийный номер: {SerialNumber}", existing.Id, existing.SerialNumber);
        return existing;
    }

    private async Task<Boiler> CreateNewAsync(
        AppDbContext dbContext,
        string serialNumber,
        long boilerTypeCycleId,
        string operatorName)
    {
        var boiler = new Boiler
        {
            SerialNumber = serialNumber,
            BoilerTypeCycleId = boilerTypeCycleId,
            DateCreate = DateTime.Now,
            DateUpdate = null,
            Status = OperationResultStatus.InWork,
            Operator = operatorName
        };
        dbContext.Boilers.Add(boiler);
        await dbContext.SaveChangesAsync();
        logger.LogInformation("Created Boiler {Id}, SerialNumber: {SerialNumber}", boiler.Id, serialNumber);
        dbLogger.LogInformation("Создан котёл {Id}, серийный номер: {SerialNumber}", boiler.Id, serialNumber);
        return boiler;
    }
}

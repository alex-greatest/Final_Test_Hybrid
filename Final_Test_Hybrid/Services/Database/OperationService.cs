using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Database.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Database;

public class OperationService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<OperationService> logger,
    IDatabaseLogger dbLogger)
{
    public async Task<Operation> CreateAsync(long boilerId, string operatorName, int shiftNumber)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        try
        {
            var operation = new Operation
            {
                BoilerId = boilerId,
                DateStart = DateTime.UtcNow,
                DateEnd = null,
                Status = OperationResultStatus.InWork,
                NumberShift = shiftNumber,
                Comment = null,
                Version = 1,
                Operator = operatorName
            };
            dbContext.Operations.Add(operation);
            await dbContext.SaveChangesAsync();
            logger.LogInformation("Created Operation {Id} for Boiler {BoilerId}", operation.Id, boilerId);
            dbLogger.LogInformation("Создана операция {Id} для котла {BoilerId}", operation.Id, boilerId);
            return operation;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create Operation for Boiler {BoilerId}", boilerId);
            dbLogger.LogError(ex, "Ошибка создания операции для котла {BoilerId}", boilerId);
            throw new InvalidOperationException(DbConstraintErrorHandler.GetUserFriendlyMessage(ex), ex);
        }
    }
}

using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Database.Config;
using Final_Test_Hybrid.Services.Storage.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Final_Test_Hybrid.Services.SpringBoot.Operation.Interrupt;

/// <summary>
/// Сервис сохранения причины прерывания в БД.
/// </summary>
public class InterruptReasonStorageService(
    IDbContextFactory<AppDbContext> contextFactory,
    IOperationStorageService operationStorage,
    IResultStorageService resultStorage,
    IErrorStorageService errorStorage,
    IStepTimeStorageService stepTimeStorage,
    DualLogger<InterruptReasonStorageService> logger)
{
    public async Task<SaveResult> SaveAsync(
        string serialNumber,
        string adminUsername,
        string reason,
        CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
        {
            return SaveResult.Fail("Операция отменена");
        }
        try
        {
            return await ExecuteSaveAsync(serialNumber, adminUsername, reason, ct);
        }
        catch (OperationCanceledException)
        {
            return SaveResult.Fail("Операция отменена");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка сохранения в БД");
            return SaveResult.Fail("Ошибка базы данных");
        }
    }

    private async Task<SaveResult> ExecuteSaveAsync(
        string serialNumber,
        string adminUsername,
        string reason,
        CancellationToken ct)
    {
        await using var context = await contextFactory.CreateDbContextAsync(ct);

        var operation = await operationStorage.UpdateInterruptedOperationAsync(
            context,
            serialNumber,
            adminUsername,
            reason,
            ct);
        if (operation == null)
        {
            return SaveResult.Fail("Операция не найдена");
        }

        var results = await resultStorage.CreateResultsAsync(context, operation, ct);
        AddEntities(context.Results, results);

        var errors = await errorStorage.CreateErrorsAsync(context, operation, ct);
        AddEntities(context.Errors, errors);

        var stepTimes = await stepTimeStorage.CreateStepTimesAsync(context, operation, ct);
        AddEntities(context.StepTimes, stepTimes);

        await SaveAllChangesAsync(context, operation, results, errors, stepTimes, ct);

        return SaveResult.Success();
    }

    private static void AddEntities<TEntity>(DbSet<TEntity> dbSet, List<TEntity> entities)
        where TEntity : class
    {
        if (entities.Count > 0)
        {
            dbSet.AddRange(entities);
        }
    }

    private async Task SaveAllChangesAsync(
        AppDbContext context,
        Models.Database.Operation operation,
        List<Result> results,
        List<Error> errors,
        List<StepTime> stepTimes,
        CancellationToken ct)
    {
        await context.SaveChangesAsync(ct);
        logger.LogInformation(
            "Причина прерывания и snapshot сохранены: Operation={OperationId}, Results={ResultCount}, Errors={ErrorCount}, StepTimes={StepTimeCount}",
            operation.Id,
            results.Count,
            errors.Count,
            stepTimes.Count);
    }
}

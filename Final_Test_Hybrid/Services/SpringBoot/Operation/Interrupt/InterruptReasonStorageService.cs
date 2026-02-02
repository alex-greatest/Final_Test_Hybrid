using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Database.Config;
using Final_Test_Hybrid.Services.SpringBoot.Operation.Interrupt;
using Microsoft.EntityFrameworkCore;

namespace Final_Test_Hybrid.Services.SpringBoot.Operation.Interrupt;

/// <summary>
/// Сервис сохранения причины прерывания в БД.
/// </summary>
public class InterruptReasonStorageService(
    IDbContextFactory<AppDbContext> contextFactory,
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

        var operation = await FindOperationAsync(context, serialNumber, ct);
        if (operation == null)
        {
            return SaveResult.Fail("Операция не найдена");
        }
        UpdateOperation(operation, adminUsername, reason);
        await context.SaveChangesAsync(ct);

        return SaveResult.Success();
    }

    private static void UpdateOperation(Models.Database.Operation operation, string adminUsername, string reason)
    {
        operation.Status = OperationResultStatus.Interrupted;
        operation.Comment = reason;
        operation.AdminInterrupted = adminUsername;
        operation.DateEnd = DateTime.UtcNow;
    }

    private static async Task<Models.Database.Operation?> FindOperationAsync(
        AppDbContext context,
        string serialNumber,
        CancellationToken ct)
    {
        var boiler = await FindBoilerAsync(context, serialNumber, ct);
        return boiler == null ? null : await FindLastOperationAsync(context, boiler.Id, ct);
    }

    private static Task<Boiler?> FindBoilerAsync(
        AppDbContext context,
        string serialNumber,
        CancellationToken ct)
    {
        return context.Boilers.FirstOrDefaultAsync(b => b.SerialNumber == serialNumber, ct);
    }

    private static Task<Models.Database.Operation?> FindLastOperationAsync(
        AppDbContext context,
        long boilerId,
        CancellationToken ct)
    {
        return context.Operations
            .Where(o => o.BoilerId == boilerId)
            .OrderByDescending(o => o.DateStart)
            .FirstOrDefaultAsync(ct);
    }
}

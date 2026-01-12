using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Database.Config;
using Microsoft.EntityFrameworkCore;

namespace Final_Test_Hybrid.Services.Database;

public class ResultSettingsService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    DualLogger<ResultSettingsService> logger)
{
    public async Task<List<ResultSettings>> GetAllAsync()
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.ResultSettings
            .Include(r => r.BoilerType)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<ResultSettings> CreateAsync(ResultSettings settings)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        try
        {
            dbContext.ResultSettings.Add(settings);
            await dbContext.SaveChangesAsync();
            AddActiveHistory(dbContext, settings);
            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            logger.LogInformation("Созданы настройки результата {Id} с активной историей", settings.Id);
            return settings;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            logger.LogError(ex, "Ошибка создания настроек результата");
            throw new InvalidOperationException(DbConstraintErrorHandler.GetUserFriendlyMessage(ex), ex);
        }
    }

    public async Task UpdateAsync(ResultSettings settings)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        try
        {
            var existing = await dbContext.ResultSettings.FirstOrDefaultAsync(x => x.Id == settings.Id);
            if (existing == null)
            {
                throw new InvalidOperationException("Настройка результата не найдена");
            }
            CopySettingsProperties(settings, existing);
            await DeactivateCurrentHistory(dbContext, settings.Id);
            AddActiveHistory(dbContext, settings);
            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            logger.LogInformation("Обновлены настройки результата {Id} с новой активной историей", settings.Id);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            logger.LogError(ex, "Ошибка обновления настроек результата {Id}", settings.Id);
            throw new InvalidOperationException(DbConstraintErrorHandler.GetUserFriendlyMessage(ex), ex);
        }
    }

    public async Task DeleteAsync(long id)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        try
        {
            var settings = await dbContext.ResultSettings.FirstOrDefaultAsync(x => x.Id == id);
            if (settings == null)
            {
                return;
            }
            await DeactivateCurrentHistory(dbContext, id);
            dbContext.ResultSettings.Remove(settings);
            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            logger.LogInformation("Удалены настройки результата {Id}", id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка удаления настроек результата {Id}", id);
            throw new InvalidOperationException(DbConstraintErrorHandler.GetUserFriendlyMessage(ex), ex);
        }
    }

    public async Task<List<string>> CopyToBoilerTypeAsync(
        IEnumerable<ResultSettings> items,
        long targetBoilerTypeId)
    {
        var failedItems = new List<string>();
        foreach (var item in items)
        {
            var success = await TryCopySingleItemAsync(item, targetBoilerTypeId);
            if (!success)
            {
                failedItems.Add(item.ParameterName);
            }
        }
        return failedItems;
    }

    public async Task ReplaceForBoilerTypeAsync(
        long boilerTypeId,
        List<ResultSettings> items,
        CancellationToken ct = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            await DeleteExistingWithHistoryAsync(dbContext, boilerTypeId, ct);
            await AddNewWithHistoryAsync(dbContext, items, boilerTypeId, ct);
            await transaction.CommitAsync(ct);
            logger.LogInformation("Заменено {Count} настроек результата для типа котла {BoilerTypeId}",
                items.Count, boilerTypeId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            logger.LogError(ex, "Ошибка замены настроек результата для типа котла {BoilerTypeId}", boilerTypeId);
            throw new InvalidOperationException(DbConstraintErrorHandler.GetUserFriendlyMessage(ex), ex);
        }
    }

    public async Task DeleteAllByBoilerTypeAsync(long boilerTypeId, CancellationToken ct = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            await DeleteExistingWithHistoryAsync(dbContext, boilerTypeId, ct);
            await transaction.CommitAsync(ct);
            logger.LogInformation("Удалены все настройки результата для типа котла {Id}", boilerTypeId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            logger.LogError(ex, "Ошибка удаления всех настроек результата для типа котла {Id}", boilerTypeId);
            throw new InvalidOperationException(DbConstraintErrorHandler.GetUserFriendlyMessage(ex), ex);
        }
    }

    private async Task DeleteExistingWithHistoryAsync(AppDbContext dbContext, long boilerTypeId, CancellationToken ct)
    {
        var existingIds = await dbContext.ResultSettings
            .Where(r => r.BoilerTypeId == boilerTypeId)
            .Select(r => r.Id)
            .ToListAsync(ct);
        foreach (var id in existingIds)
        {
            await DeactivateCurrentHistory(dbContext, id);
        }
        await dbContext.SaveChangesAsync(ct);
        var deleted = await dbContext.ResultSettings
            .Where(r => r.BoilerTypeId == boilerTypeId)
            .ExecuteDeleteAsync(ct);
        logger.LogInformation("Удалено {Count} настроек результата для типа котла {BoilerTypeId}", deleted, boilerTypeId);
    }

    private async Task AddNewWithHistoryAsync(
        AppDbContext dbContext,
        List<ResultSettings> items,
        long boilerTypeId,
        CancellationToken ct)
    {
        foreach (var item in items)
        {
            item.BoilerTypeId = boilerTypeId;
            dbContext.ResultSettings.Add(item);
        }
        await dbContext.SaveChangesAsync(ct);
        foreach (var item in items)
        {
            AddActiveHistory(dbContext, item);
        }
        await dbContext.SaveChangesAsync(ct);
    }

    private async Task<bool> TryCopySingleItemAsync(ResultSettings source, long targetBoilerTypeId)
    {
        try
        {
            await CopySingleItemAsync(source, targetBoilerTypeId);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка копирования настроек результата {ParameterName}", source.ParameterName);
            return false;
        }
    }

    private async Task CopySingleItemAsync(ResultSettings source, long targetBoilerTypeId)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        var copy = CreateCopyForBoilerType(source, targetBoilerTypeId);
        dbContext.ResultSettings.Add(copy);
        await dbContext.SaveChangesAsync();
        AddActiveHistory(dbContext, copy);
        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
        logger.LogInformation("Скопированы настройки результата {ParameterName} в тип котла {BoilerTypeId}",
            source.ParameterName, targetBoilerTypeId);
    }

    private static ResultSettings CreateCopyForBoilerType(ResultSettings source, long targetBoilerTypeId) =>
        new()
        {
            BoilerTypeId = targetBoilerTypeId,
            ParameterName = source.ParameterName,
            AddressValue = source.AddressValue,
            AddressMin = source.AddressMin,
            AddressMax = source.AddressMax,
            AddressStatus = source.AddressStatus,
            Nominal = source.Nominal,
            PlcType = source.PlcType,
            Unit = source.Unit,
            Description = source.Description,
            AuditType = source.AuditType
        };

    private static void AddActiveHistory(AppDbContext dbContext, ResultSettings settings)
    {
        var history = new ResultSettingHistory
        {
            ResultsSettingsId = settings.Id,
            BoilerTypeId = settings.BoilerTypeId,
            ParameterName = settings.ParameterName,
            AddressValue = settings.AddressValue,
            AddressMin = settings.AddressMin,
            AddressMax = settings.AddressMax,
            AddressStatus = settings.AddressStatus,
            Nominal = settings.Nominal,
            PlcType = settings.PlcType,
            Unit = settings.Unit,
            Description = settings.Description,
            AuditType = settings.AuditType,
            IsActive = true
        };
        dbContext.ResultSettingHistories.Add(history);
    }

    private static async Task DeactivateCurrentHistory(AppDbContext dbContext, long settingsId)
    {
        var existingHistory = await dbContext.ResultSettingHistories
            .FirstOrDefaultAsync(x => x.ResultsSettingsId == settingsId && x.IsActive);
        if (existingHistory != null)
        {
            existingHistory.IsActive = false;
        }
    }

    private static void CopySettingsProperties(ResultSettings source, ResultSettings target)
    {
        target.ParameterName = source.ParameterName;
        target.AddressValue = source.AddressValue;
        target.AddressMin = source.AddressMin;
        target.AddressMax = source.AddressMax;
        target.AddressStatus = source.AddressStatus;
        target.Nominal = source.Nominal;
        target.PlcType = source.PlcType;
        target.Unit = source.Unit;
        target.Description = source.Description;
        target.AuditType = source.AuditType;
        target.BoilerTypeId = source.BoilerTypeId;
    }
}

using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Services.Database.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Database;

public class ResultSettingsService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<ResultSettingsService> logger)
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
            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            logger.LogInformation("Created ResultSettings {Id} with active history", settings.Id);
            return settings;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            logger.LogError(ex, "Failed to create ResultSettings");
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
            existing.ParameterName = settings.ParameterName;
            existing.AddressValue = settings.AddressValue;
            existing.AddressMin = settings.AddressMin;
            existing.AddressMax = settings.AddressMax;
            existing.AddressStatus = settings.AddressStatus;
            existing.Nominal = settings.Nominal;
            existing.PlcType = settings.PlcType;
            existing.Unit = settings.Unit;
            existing.Description = settings.Description;
            existing.AuditType = settings.AuditType;
            existing.BoilerTypeId = settings.BoilerTypeId;
            var existingHistory = await dbContext.ResultSettingHistories
                .FirstOrDefaultAsync(x => x.ResultsSettingsId == settings.Id && x.IsActive);
            existingHistory?.IsActive = false;
            var newHistory = new ResultSettingHistory
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
            dbContext.ResultSettingHistories.Add(newHistory);
            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            logger.LogInformation("Updated ResultSettings {Id} with new active history", settings.Id);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            logger.LogError(ex, "Failed to update ResultSettings {Id}", settings.Id);
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
            var activeHistory = await dbContext.ResultSettingHistories
                .FirstOrDefaultAsync(x => x.ResultsSettingsId == id && x.IsActive);
            activeHistory?.IsActive = false;
            dbContext.ResultSettings.Remove(settings);
            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            logger.LogInformation("Deleted ResultSettings {Id}", id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete ResultSettings {Id}", id);
            throw new InvalidOperationException(DbConstraintErrorHandler.GetUserFriendlyMessage(ex), ex);
        }
    }
}

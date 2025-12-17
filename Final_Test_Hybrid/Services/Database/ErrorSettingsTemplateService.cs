using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Services.Database.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Database;

public class ErrorSettingsTemplateService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<ErrorSettingsTemplateService> logger)
{
    public async Task<List<ErrorSettingsTemplate>> GetAllAsync()
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.ErrorSettingsTemplates
            .Include(e => e.Step)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<ErrorSettingsTemplate> CreateAsync(ErrorSettingsTemplate template)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        try
        {
            AddTemplate(dbContext, template);
            await dbContext.SaveChangesAsync();
            AddActiveHistory(dbContext, template);
            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            logger.LogInformation("Created ErrorSettingsTemplate {Id} with active history", template.Id);
            return template;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            logger.LogError(ex, "Failed to create ErrorSettingsTemplate");
            throw new InvalidOperationException(DbConstraintErrorHandler.GetUserFriendlyMessage(ex), ex);
        }
    }

    public async Task UpdateAsync(ErrorSettingsTemplate template)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        try
        {
            var existing = await FindTemplateOrThrowAsync(dbContext, template.Id);
            UpdateTemplateProperties(existing, template);
            await DeactivateCurrentHistoryAsync(dbContext, template.Id);
            AddActiveHistory(dbContext, template);
            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            logger.LogInformation("Updated ErrorSettingsTemplate {Id} with new active history", template.Id);
        }
        catch (InvalidOperationException)
        {
            await transaction.RollbackAsync();
            throw;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            logger.LogError(ex, "Failed to update ErrorSettingsTemplate {Id}", template.Id);
            throw new InvalidOperationException(DbConstraintErrorHandler.GetUserFriendlyMessage(ex), ex);
        }
    }

    public async Task DeleteAsync(long id)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        try
        {
            var template = await dbContext.ErrorSettingsTemplates.FirstOrDefaultAsync(x => x.Id == id);
            if (template == null)
            {
                return;
            }
            await DeactivateCurrentHistoryAsync(dbContext, id);
            dbContext.ErrorSettingsTemplates.Remove(template);
            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            logger.LogInformation("Deleted ErrorSettingsTemplate {Id}", id);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            logger.LogError(ex, "Failed to delete ErrorSettingsTemplate {Id}", id);
            throw new InvalidOperationException(DbConstraintErrorHandler.GetUserFriendlyMessage(ex), ex);
        }
    }

    private static void AddTemplate(AppDbContext dbContext, ErrorSettingsTemplate template)
    {
        dbContext.ErrorSettingsTemplates.Add(template);
    }

    private static void AddActiveHistory(AppDbContext dbContext, ErrorSettingsTemplate template)
    {
        var history = new ErrorSettingsHistory
        {
            ErrorSettingsTemplateId = template.Id,
            StepHistoryId = null,
            AddressError = template.AddressError,
            Description = template.Description,
            IsActive = true
        };
        dbContext.ErrorSettingsHistories.Add(history);
    }

    private static async Task<ErrorSettingsTemplate> FindTemplateOrThrowAsync(AppDbContext dbContext, long id)
    {
        var template = await dbContext.ErrorSettingsTemplates.FirstOrDefaultAsync(x => x.Id == id);
        return template ?? throw new InvalidOperationException("Шаблон настроек ошибок не найден");
    }

    private static void UpdateTemplateProperties(ErrorSettingsTemplate existing, ErrorSettingsTemplate updated)
    {
        existing.StepId = updated.StepId;
        existing.AddressError = updated.AddressError;
        existing.Description = updated.Description;
    }

    private static async Task DeactivateCurrentHistoryAsync(AppDbContext dbContext, long templateId)
    {
        var existingHistory = await dbContext.ErrorSettingsHistories
            .FirstOrDefaultAsync(x => x.ErrorSettingsTemplateId == templateId && x.IsActive);
        existingHistory?.IsActive = false;
    }
}

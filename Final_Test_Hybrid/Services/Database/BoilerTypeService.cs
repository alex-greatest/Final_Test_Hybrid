using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Database.Config;
using Microsoft.EntityFrameworkCore;

namespace Final_Test_Hybrid.Services.Database;

public class BoilerTypeService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    DualLogger<BoilerTypeService> logger)
{
    public async Task<List<BoilerType>> GetAllAsync()
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.BoilerTypes.AsNoTracking().ToListAsync();
    }

    public async Task<BoilerTypeCycle?> FindActiveByArticleAsync(string article)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            return await dbContext.BoilerTypeCycles
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Article == article && x.IsActive);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка поиска активного типа котла по артикулу {Article}", article);
            throw new InvalidOperationException("Ошибка БД", ex);
        }
    }

    public async Task<BoilerType> CreateAsync(BoilerType boilerType)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        try
        {
            dbContext.BoilerTypes.Add(boilerType);
            await dbContext.SaveChangesAsync();
            var cycle = new BoilerTypeCycle
            {
                BoilerTypeId = boilerType.Id,
                Type = boilerType.Type,
                Article = boilerType.Article,
                IsActive = true
            };
            dbContext.BoilerTypeCycles.Add(cycle);
            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            logger.LogInformation("Создан тип котла {Id} с активным циклом", boilerType.Id);
            return boilerType;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            logger.LogError(ex, "Ошибка создания типа котла");
            throw new InvalidOperationException(DbConstraintErrorHandler.GetUserFriendlyMessage(ex), ex);
        }
    }

    public async Task UpdateAsync(BoilerType boilerType)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        try
        {
            var existing = await dbContext.BoilerTypes.FirstOrDefaultAsync(x => x.Id == boilerType.Id);
            if (existing == null)
            {
                throw new InvalidOperationException("Тип котла не найден");
            }
            existing.Article = boilerType.Article;
            existing.Type = boilerType.Type;
            var existingCycle = await dbContext.BoilerTypeCycles
                .FirstOrDefaultAsync(x => x.BoilerTypeId == boilerType.Id && x.IsActive);
            existingCycle?.IsActive = false;
            var newCycle = new BoilerTypeCycle
            {
                BoilerTypeId = boilerType.Id,
                Type = boilerType.Type,
                Article = boilerType.Article,
                IsActive = true
            };
            dbContext.BoilerTypeCycles.Add(newCycle);
            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            logger.LogInformation("Обновлён тип котла {Id} с новым активным циклом", boilerType.Id);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            logger.LogError(ex, "Ошибка обновления типа котла {Id}", boilerType.Id);
            throw new InvalidOperationException(DbConstraintErrorHandler.GetUserFriendlyMessage(ex), ex);
        }
    }

    public async Task DeleteAsync(long id)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        try
        {
            var boilerType = await dbContext.BoilerTypes.FirstOrDefaultAsync(x => x.Id == id);
            if (boilerType == null)
            {
                return;
            }
            var activeCycle = await dbContext.BoilerTypeCycles
                .FirstOrDefaultAsync(x => x.BoilerTypeId == id && x.IsActive);
            activeCycle?.IsActive = false;
            dbContext.BoilerTypes.Remove(boilerType);
            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            logger.LogInformation("Удалён тип котла {Id}", id);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            logger.LogError(ex, "Ошибка удаления типа котла {Id}", id);
            throw new InvalidOperationException(DbConstraintErrorHandler.GetUserFriendlyMessage(ex), ex);
        }
    }

    public async Task DeleteAllAsync(CancellationToken ct = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            await dbContext.BoilerTypeCycles
                .Where(c => c.IsActive)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsActive, false), ct);
            await dbContext.BoilerTypes.ExecuteDeleteAsync(ct);
            await transaction.CommitAsync(ct);
            logger.LogInformation("Удалены все типы котлов");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            logger.LogError(ex, "Ошибка удаления всех типов котлов");
            throw new InvalidOperationException(DbConstraintErrorHandler.GetUserFriendlyMessage(ex), ex);
        }
    }
}

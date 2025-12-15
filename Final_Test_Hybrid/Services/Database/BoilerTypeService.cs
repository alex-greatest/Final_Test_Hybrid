using Final_Test_Hybrid.Models.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Database;

public class BoilerTypeService(
    AppDbContext dbContext,
    ILogger<BoilerTypeService> logger)
{
    public async Task<List<BoilerType>> GetAllAsync()
    {
        return await dbContext.BoilerTypes.AsNoTracking().ToListAsync();
    }

    public async Task<BoilerType?> GetByIdAsync(long id)
    {
        return await dbContext.BoilerTypes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<BoilerType> CreateAsync(BoilerType boilerType)
    {
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
            logger.LogInformation("Created BoilerType {Id} with active cycle", boilerType.Id);
            return boilerType;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            logger.LogError(ex, "Failed to create BoilerType");
            throw;
        }
    }

    public async Task<BoilerType> UpdateAsync(BoilerType boilerType)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        try
        {
            var existingCycle = await dbContext.BoilerTypeCycles
                .FirstOrDefaultAsync(x => x.BoilerTypeId == boilerType.Id && x.IsActive);
            if (existingCycle != null)
            {
                existingCycle.IsActive = false;
            }
            var newCycle = new BoilerTypeCycle
            {
                BoilerTypeId = boilerType.Id,
                Type = boilerType.Type,
                Article = boilerType.Article,
                IsActive = true
            };
            dbContext.BoilerTypeCycles.Add(newCycle);
            dbContext.BoilerTypes.Update(boilerType);
            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            logger.LogInformation("Updated BoilerType {Id} with new active cycle", boilerType.Id);
            return boilerType;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            logger.LogError(ex, "Failed to update BoilerType {Id}", boilerType.Id);
            throw;
        }
    }

    public async Task DeleteAsync(long id)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        try
        {
            var activeCycle = await dbContext.BoilerTypeCycles
                .FirstOrDefaultAsync(x => x.BoilerTypeId == id && x.IsActive);
            activeCycle?.IsActive = false;
            var boilerType = await dbContext.BoilerTypes.FirstOrDefaultAsync(x => x.Id == id);
            if (boilerType != null)
            {
                dbContext.BoilerTypes.Remove(boilerType);
            }
            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            logger.LogInformation("Deleted BoilerType {Id}", id);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            logger.LogError(ex, "Failed to delete BoilerType {Id}", id);
            throw;
        }
    }
}

using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Services.Database.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Database;

public class RecipeService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<RecipeService> logger)
{
    public async Task<List<Recipe>> GetAllAsync()
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.Recipes
            .Include(r => r.BoilerType)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<List<Recipe>> GetByBoilerTypeIdAsync(long boilerTypeId)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.Recipes
            .Include(r => r.BoilerType)
            .Where(r => r.BoilerTypeId == boilerTypeId)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Recipe> CreateAsync(Recipe recipe)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        try
        {
            dbContext.Recipes.Add(recipe);
            await dbContext.SaveChangesAsync();
            logger.LogInformation("Created Recipe {Id} for BoilerType {BoilerTypeId}", recipe.Id, recipe.BoilerTypeId);
            return recipe;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create Recipe");
            throw new InvalidOperationException(DbConstraintErrorHandler.GetUserFriendlyMessage(ex), ex);
        }
    }

    public async Task<Recipe> UpdateAsync(Recipe recipe)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        try
        {
            dbContext.Recipes.Update(recipe);
            await dbContext.SaveChangesAsync();
            logger.LogInformation("Updated Recipe {Id}", recipe.Id);
            return recipe;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update Recipe {Id}", recipe.Id);
            throw new InvalidOperationException(DbConstraintErrorHandler.GetUserFriendlyMessage(ex), ex);
        }
    }

    public async Task DeleteAsync(long id)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        try
        {
            var recipe = await dbContext.Recipes.FirstOrDefaultAsync(r => r.Id == id);
            if (recipe != null)
            {
                dbContext.Recipes.Remove(recipe);
                await dbContext.SaveChangesAsync();
                logger.LogInformation("Deleted Recipe {Id}", id);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete Recipe {Id}", id);
            throw new InvalidOperationException(DbConstraintErrorHandler.GetUserFriendlyMessage(ex), ex);
        }
    }
}

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

    public async Task UpdateAsync(Recipe recipe)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        try
        {
            var existing = await dbContext.Recipes.FirstOrDefaultAsync(r => r.Id == recipe.Id);
            if (existing == null)
            {
                throw new InvalidOperationException("Рецепт не найден");
            }
            existing.BoilerTypeId = recipe.BoilerTypeId;
            existing.PlcType = recipe.PlcType;
            existing.IsPlc = recipe.IsPlc;
            existing.Address = recipe.Address;
            existing.TagName = recipe.TagName;
            existing.Value = recipe.Value;
            existing.Description = recipe.Description;
            existing.Unit = recipe.Unit;
            await dbContext.SaveChangesAsync();
            logger.LogInformation("Updated Recipe {Id}", recipe.Id);
        }
        catch (InvalidOperationException)
        {
            throw;
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
            var deleted = await dbContext.Recipes.Where(r => r.Id == id).ExecuteDeleteAsync();
            if (deleted > 0)
            {
                logger.LogInformation("Deleted Recipe {Id}", id);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete Recipe {Id}", id);
            throw new InvalidOperationException(DbConstraintErrorHandler.GetUserFriendlyMessage(ex), ex);
        }
    }

    public async Task<List<string>> CopyRecipesToBoilerTypeAsync(
        IEnumerable<Recipe> recipes,
        long targetBoilerTypeId)
    {
        var failedRecipes = new List<string>();
        foreach (var recipe in recipes)
        {
            try
            {
                await using var dbContext = await dbContextFactory.CreateDbContextAsync();
                var copy = new Recipe
                {
                    BoilerTypeId = targetBoilerTypeId,
                    PlcType = recipe.PlcType,
                    IsPlc = recipe.IsPlc,
                    Address = recipe.Address,
                    TagName = recipe.TagName,
                    Value = recipe.Value,
                    Description = recipe.Description,
                    Unit = recipe.Unit
                };
                dbContext.Recipes.Add(copy);
                await dbContext.SaveChangesAsync();
                logger.LogInformation("Copied Recipe {TagName} to BoilerType {BoilerTypeId}", recipe.TagName, targetBoilerTypeId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to copy Recipe {TagName} to BoilerType {BoilerTypeId}", recipe.TagName, targetBoilerTypeId);
                failedRecipes.Add(recipe.TagName);
            }
        }
        return failedRecipes;
    }
}

using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Database.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Database;

public class RecipeService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<RecipeService> logger,
    IDatabaseLogger dbLogger)
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
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            return await dbContext.Recipes
                .Where(r => r.BoilerTypeId == boilerTypeId)
                .AsNoTracking()
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get recipes by BoilerTypeId {BoilerTypeId}", boilerTypeId);
            dbLogger.LogError(ex, "Ошибка получения рецептов по типу котла {BoilerTypeId}", boilerTypeId);
            throw new InvalidOperationException("Ошибка БД", ex);
        }
    }

    public async Task<Recipe> CreateAsync(Recipe recipe)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        try
        {
            dbContext.Recipes.Add(recipe);
            await dbContext.SaveChangesAsync();
            logger.LogInformation("Created Recipe {Id} for BoilerType {BoilerTypeId}", recipe.Id, recipe.BoilerTypeId);
            dbLogger.LogInformation("Создан рецепт {Id} для типа котла {BoilerTypeId}", recipe.Id, recipe.BoilerTypeId);
            return recipe;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create Recipe");
            dbLogger.LogError(ex, "Ошибка создания рецепта");
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
            dbLogger.LogInformation("Обновлён рецепт {Id}", recipe.Id);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update Recipe {Id}", recipe.Id);
            dbLogger.LogError(ex, "Ошибка обновления рецепта {Id}", recipe.Id);
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
                dbLogger.LogInformation("Удалён рецепт {Id}", id);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete Recipe {Id}", id);
            dbLogger.LogError(ex, "Ошибка удаления рецепта {Id}", id);
            throw new InvalidOperationException(DbConstraintErrorHandler.GetUserFriendlyMessage(ex), ex);
        }
    }

    public async Task DeleteAllByBoilerTypeAsync(long boilerTypeId, CancellationToken ct = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);
        try
        {
            await dbContext.Recipes
                .Where(r => r.BoilerTypeId == boilerTypeId)
                .ExecuteDeleteAsync(ct);
            logger.LogInformation("Deleted all Recipes for BoilerType {Id}", boilerTypeId);
            dbLogger.LogInformation("Удалены все рецепты для типа котла {Id}", boilerTypeId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete all Recipes for BoilerType {Id}", boilerTypeId);
            dbLogger.LogError(ex, "Ошибка удаления всех рецептов для типа котла {Id}", boilerTypeId);
            throw new InvalidOperationException(DbConstraintErrorHandler.GetUserFriendlyMessage(ex), ex);
        }
    }

    public async Task ReplaceRecipesForBoilerTypeAsync(
        long boilerTypeId,
        List<Recipe> recipes,
        CancellationToken ct = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            var deleted = await dbContext.Recipes
                .Where(r => r.BoilerTypeId == boilerTypeId)
                .ExecuteDeleteAsync(ct);
            logger.LogInformation("Deleted {Count} recipes for BoilerType {BoilerTypeId}", deleted, boilerTypeId);
            dbLogger.LogInformation("Удалено {Count} рецептов для типа котла {BoilerTypeId}", deleted, boilerTypeId);
            foreach (var recipe in recipes)
            {
                recipe.BoilerTypeId = boilerTypeId;
                dbContext.Recipes.Add(recipe);
            }
            await dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            logger.LogInformation("Added {Count} recipes for BoilerType {BoilerTypeId}", recipes.Count, boilerTypeId);
            dbLogger.LogInformation("Добавлено {Count} рецептов для типа котла {BoilerTypeId}", recipes.Count, boilerTypeId);
        }
        catch (OperationCanceledException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            logger.LogError(ex, "Failed to replace recipes for BoilerType {BoilerTypeId}", boilerTypeId);
            dbLogger.LogError(ex, "Ошибка замены рецептов для типа котла {BoilerTypeId}", boilerTypeId);
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
                dbLogger.LogInformation("Скопирован рецепт {TagName} в тип котла {BoilerTypeId}", recipe.TagName, targetBoilerTypeId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to copy Recipe {TagName} to BoilerType {BoilerTypeId}", recipe.TagName, targetBoilerTypeId);
                dbLogger.LogError(ex, "Ошибка копирования рецепта {TagName} в тип котла {BoilerTypeId}", recipe.TagName, targetBoilerTypeId);
                failedRecipes.Add(recipe.TagName);
            }
        }
        return failedRecipes;
    }
}

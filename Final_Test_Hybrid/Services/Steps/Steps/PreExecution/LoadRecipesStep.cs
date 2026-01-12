using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Database;
using Final_Test_Hybrid.Services.SpringBoot.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;

namespace Final_Test_Hybrid.Services.Steps.Steps.PreExecution;

public class LoadRecipesStep(
    RecipeService recipeService,
    DualLogger<LoadRecipesStep> logger) : IPreExecutionStep
{
    public string Id => "load-recipes";
    public string Name => "Загрузка рецептов";
    public string Description => "Загрузка рецептов из базы данных";
    public bool IsVisibleInStatusGrid => false;

    public async Task<PreExecutionResult> ExecuteAsync(PreExecutionContext context, CancellationToken ct)
    {
        var boilerTypeId = context.BoilerTypeCycle!.BoilerTypeId;
        var recipes = await recipeService.GetByBoilerTypeIdAsync(boilerTypeId);
        if (recipes.Count == 0)
        {
            return PreExecutionResult.Fail("Рецепты не найдены");
        }
        context.Recipes = MapToRecipeResponseDtos(recipes);
        LogRecipes(context.Recipes);
        return PreExecutionResult.Continue();
    }

    private void LogRecipes(IReadOnlyList<RecipeResponseDto> recipes)
    {
        logger.LogInformation("Загружено рецептов: {Count}", recipes.Count);
        foreach (var recipe in recipes)
        {
            logger.LogInformation("Рецепт: {TagName} = {Value} ({PlcType})",
                recipe.TagName, recipe.Value, recipe.PlcType);
        }
    }

    private static IReadOnlyList<RecipeResponseDto> MapToRecipeResponseDtos(List<Recipe> recipes)
    {
        return recipes.Select(r => new RecipeResponseDto
        {
            TagName = r.TagName,
            Value = r.Value,
            Address = r.Address,
            PlcType = r.PlcType,
            IsPlc = r.IsPlc,
            Unit = r.Unit,
            Description = r.Description
        }).ToList();
    }
}

using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Database;
using Final_Test_Hybrid.Services.SpringBoot.Recipe;

namespace Final_Test_Hybrid.Services.Preparation;

public class BoilerDataLoader(
    BoilerTypeService boilerTypeService,
    RecipeService recipeService,
    DualLogger<BoilerDataLoader> logger) : IBoilerDataLoader
{
    public async Task<BoilerDataLoadResult> LoadAsync(string article)
    {
        var cycle = await boilerTypeService.FindActiveByArticleAsync(article);
        if (cycle == null)
        {
            return new BoilerDataLoadResult(false, $"Тип котла не найден для артикула: {article}", null, null);
        }

        logger.LogInformation("Тип котла найден: {Type}, артикул: {Article}", cycle.Type, cycle.Article);

        var recipes = await recipeService.GetByBoilerTypeIdAsync(cycle.BoilerTypeId);
        if (recipes.Count == 0)
        {
            return new BoilerDataLoadResult(false, "Рецепты не найдены", null, null);
        }

        var dtos = MapToRecipeResponseDtos(recipes);
        LogRecipes(dtos);

        return new BoilerDataLoadResult(true, null, cycle, dtos);
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

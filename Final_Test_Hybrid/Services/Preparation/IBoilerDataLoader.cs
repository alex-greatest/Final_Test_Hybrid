using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Services.SpringBoot.Recipe;

namespace Final_Test_Hybrid.Services.Preparation;

public record BoilerDataLoadResult(
    bool IsSuccess,
    string? Error,
    BoilerTypeCycle? BoilerTypeCycle,
    IReadOnlyList<RecipeResponseDto>? Recipes);

public interface IBoilerDataLoader
{
    Task<BoilerDataLoadResult> LoadAsync(string article);
}

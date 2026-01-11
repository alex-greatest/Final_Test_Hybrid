using Final_Test_Hybrid.Services.SpringBoot.Recipe;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;

public interface IRecipeProvider
{
    RecipeResponseDto? GetByAddress(string address);
    IReadOnlyList<RecipeResponseDto> GetAll();
    void SetRecipes(IReadOnlyList<RecipeResponseDto> recipes);
    void Clear();
    T? GetValue<T>(string address) where T : struct;
    string? GetStringValue(string address);
}

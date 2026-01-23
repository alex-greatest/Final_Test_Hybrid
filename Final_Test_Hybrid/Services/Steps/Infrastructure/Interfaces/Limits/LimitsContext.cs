using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Limits;

/// <summary>
/// Контекст для получения пределов перед выполнением шага.
/// </summary>
public class LimitsContext
{
    /// <summary>
    /// Индекс колонки (0-3).
    /// </summary>
    public required int ColumnIndex { get; init; }

    /// <summary>
    /// Провайдер рецептов для получения значений пределов.
    /// </summary>
    public required IRecipeProvider RecipeProvider { get; init; }
}

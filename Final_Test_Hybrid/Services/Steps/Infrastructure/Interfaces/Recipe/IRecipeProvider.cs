using Final_Test_Hybrid.Services.SpringBoot.Recipe;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;

/// <summary>
/// Предоставляет доступ к рецептам теста.
/// Потокобезопасный интерфейс для чтения и обновления рецептов.
/// </summary>
public interface IRecipeProvider
{
    /// <summary>
    /// Получает рецепт по адресу.
    /// </summary>
    /// <param name="address">Адрес рецепта.</param>
    /// <returns>Рецепт или null, если не найден.</returns>
    RecipeResponseDto? GetByAddress(string address);

    /// <summary>
    /// Получает все загруженные рецепты.
    /// </summary>
    /// <returns>Коллекция всех рецептов.</returns>
    IReadOnlyList<RecipeResponseDto> GetAll();

    /// <summary>
    /// Устанавливает новый набор рецептов, заменяя предыдущий.
    /// При дубликатах адресов последний рецепт побеждает.
    /// </summary>
    /// <param name="recipes">Список рецептов для загрузки.</param>
    /// <exception cref="ArgumentNullException">Если <paramref name="recipes"/> равен null.</exception>
    void SetRecipes(IReadOnlyList<RecipeResponseDto> recipes);

    /// <summary>
    /// Очищает все загруженные рецепты.
    /// </summary>
    void Clear();

    /// <summary>
    /// Получает значение рецепта, конвертированное в указанный тип.
    /// Поддерживает float, double, int, short, bool.
    /// </summary>
    /// <typeparam name="T">Тип значения (struct).</typeparam>
    /// <param name="address">Адрес рецепта.</param>
    /// <returns>Значение или null, если рецепт не найден или конвертация неудачна.</returns>
    T? GetValue<T>(string address) where T : struct;

    /// <summary>
    /// Получает строковое значение рецепта без конвертации.
    /// </summary>
    /// <param name="address">Адрес рецепта.</param>
    /// <returns>Строковое значение или null, если рецепт не найден.</returns>
    string? GetStringValue(string address);
}

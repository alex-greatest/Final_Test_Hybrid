namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Completion;

/// <summary>
/// Конфигурация для отображения RangeSlider в UI.
/// </summary>
/// <param name="Label">Название параметра для отображения.</param>
/// <param name="Unit">Единица измерения (опционально).</param>
/// <param name="ValueTag">OPC-UA тег для получения текущего значения.</param>
/// <param name="MinRecipeAddress">Адрес рецепта для минимального значения шкалы.</param>
/// <param name="MaxRecipeAddress">Адрес рецепта для максимального значения шкалы.</param>
/// <param name="GreenStartRecipeAddress">Адрес рецепта для начала зелёной зоны.</param>
/// <param name="GreenEndRecipeAddress">Адрес рецепта для конца зелёной зоны.</param>
/// <param name="TickCount">Количество делений на шкале (по умолчанию 20).</param>
public record RangeSliderConfig(
    string Label,
    string? Unit,
    string ValueTag,
    string MinRecipeAddress,
    string MaxRecipeAddress,
    string GreenStartRecipeAddress,
    string GreenEndRecipeAddress,
    int TickCount = 20);

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Completion;

/// <summary>
/// Конфигурация для отображения RangeSlider в UI.
/// </summary>
/// <param name="Label">Название параметра для отображения.</param>
/// <param name="Unit">Единица измерения (опционально).</param>
/// <param name="ValueTag">OPC-UA тег для получения текущего значения.</param>
/// <param name="GreenZoneStart">Начало зелёной зоны (число, шаг читает из рецепта).</param>
/// <param name="GreenZoneEnd">Конец зелёной зоны (число, шаг читает из рецепта).</param>
/// <param name="MinValue">Минимум шкалы (если null — вычисляется как GreenZoneStart - 50% ширины зелёной зоны).</param>
/// <param name="MaxValue">Максимум шкалы (если null — вычисляется как GreenZoneEnd + 50% ширины зелёной зоны).</param>
/// <param name="Step">Размер шага делений на шкале (по умолчанию 1).</param>
public record RangeSliderConfig(
    string Label,
    string? Unit,
    string ValueTag,
    double GreenZoneStart,
    double GreenZoneEnd,
    double? MinValue = null,
    double? MaxValue = null,
    double Step = 1);

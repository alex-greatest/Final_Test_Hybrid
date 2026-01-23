using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Limits;

/// <summary>
/// Интерфейс для шагов, предоставляющих пределы до выполнения.
/// </summary>
/// <remarks>
/// ВАЖНО: GetLimits вызывается параллельно из 4 колонок.
/// Реализация ДОЛЖНА быть thread-safe и pure (без side-effects).
/// НЕ делать IO/PLC операции - только in-memory (рецепты).
/// </remarks>
public interface IProvideLimits : ITestStep
{
    /// <summary>
    /// Получает пределы для отображения в гриде.
    /// Вызывается ПЕРЕД выполнением шага.
    /// </summary>
    /// <param name="context">Контекст с индексом колонки и провайдером рецептов.</param>
    /// <returns>Строка с пределами или null, если пределы недоступны.</returns>
    string? GetLimits(LimitsContext context);
}

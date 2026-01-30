using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Services.Steps.Steps.Coms;

/// <summary>
/// Тестовый шаг сброса котла до заводских настроек.
/// Записывает значение 1 в регистр 1057 (2.8.E Factory Reset).
/// </summary>
public class FactoryResetStep(
    IOptions<DiagnosticSettings> settings,
    DualLogger<FactoryResetStep> logger) : ITestStep
{
    private const ushort RegisterFactoryReset = 1057;
    private const ushort ResetValue = 1;

    private readonly DiagnosticSettings _settings = settings.Value;

    public string Id => "coms-factory-reset";
    public string Name => "Coms/Reset";
    public string Description => "Сброс котла до заводских настроек";

    /// <summary>
    /// Записывает значение 1 в регистр Factory Reset (1057).
    /// </summary>
    /// <param name="context">Контекст выполнения тестового шага.</param>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Запись значения {Value} в регистр {Register} (2.8.E Factory Reset)",
            ResetValue, RegisterFactoryReset);

        var modbusAddress = (ushort)(RegisterFactoryReset - _settings.BaseAddressOffset);
        var result = await context.DiagWriter.WriteUInt16Async(modbusAddress, ResetValue, ct);

        if (!result.Success)
        {
            var msg = $"Ошибка при записи значения 0x{ResetValue:X4} в регистр {RegisterFactoryReset}. {result.Error}";
            logger.LogError(msg);
            return TestStepResult.Fail(msg);
        }

        logger.LogInformation("Сброс до заводских настроек выполнен успешно");
        return TestStepResult.Pass();
    }
}

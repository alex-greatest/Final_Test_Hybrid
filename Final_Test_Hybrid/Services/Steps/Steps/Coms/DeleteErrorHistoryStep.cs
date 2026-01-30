using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Services.Steps.Steps.Coms;

/// <summary>
/// Тестовый шаг очистки журнала ошибок котла.
/// Записывает значение 0 в регистр 1154 (очистка журнала ошибок).
/// </summary>
public class DeleteErrorHistoryStep(
    IOptions<DiagnosticSettings> settings,
    DualLogger<DeleteErrorHistoryStep> logger) : ITestStep
{
    private const ushort RegisterErrorHistoryClear = 1154;
    private const ushort ClearValue = 0;

    private readonly DiagnosticSettings _settings = settings.Value;

    public string Id => "coms-delete-error-history";
    public string Name => "Coms/Delete_Error_History";
    public string Description => "Очистка журнала ошибок котла";

    /// <summary>
    /// Записывает значение 0 в регистр очистки журнала ошибок (1154).
    /// </summary>
    /// <param name="context">Контекст выполнения тестового шага.</param>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Запись значения {Value} в регистр {Register} (очистка журнала ошибок)",
            ClearValue, RegisterErrorHistoryClear);

        var modbusAddress = (ushort)(RegisterErrorHistoryClear - _settings.BaseAddressOffset);
        var result = await context.DiagWriter.WriteUInt16Async(modbusAddress, ClearValue, ct);

        if (!result.Success)
        {
            var msg = $"Ошибка записи в регистр {RegisterErrorHistoryClear}. {result.Error}";
            logger.LogError(msg);
            return TestStepResult.Fail(msg);
        }

        logger.LogInformation("Журнал ошибок очищен");
        return TestStepResult.Pass();
    }
}

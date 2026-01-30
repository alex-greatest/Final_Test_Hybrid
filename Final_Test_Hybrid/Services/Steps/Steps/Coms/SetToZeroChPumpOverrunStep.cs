using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Services.Steps.Steps.Coms;

/// <summary>
/// Тестовый шаг сброса времени выбега насоса котла.
/// Записывает значение 0 в регистр 1060 (2.9.F CH_Pump_Overrun).
/// </summary>
public class SetToZeroChPumpOverrunStep(
    IOptions<DiagnosticSettings> settings,
    DualLogger<SetToZeroChPumpOverrunStep> logger) : ITestStep
{
    private const ushort RegisterChPumpOverrun = 1060;
    private const ushort ZeroValue = 0;

    private readonly DiagnosticSettings _settings = settings.Value;

    public string Id => "coms-set-to-zero-ch-pump-overrun";
    public string Name => "Coms/Set_to_Zero_CH_Pump_Overrun";
    public string Description => "Сброс времени выбега насоса ОС";

    /// <summary>
    /// Записывает значение 0 в регистр CH_Pump_Overrun (1060).
    /// </summary>
    /// <param name="context">Контекст выполнения тестового шага.</param>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Запись значения {Value} в регистр {Register} (CH_Pump_Overrun)",
            ZeroValue, RegisterChPumpOverrun);

        var modbusAddress = (ushort)(RegisterChPumpOverrun - _settings.BaseAddressOffset);
        var result = await context.DiagWriter.WriteUInt16Async(modbusAddress, ZeroValue, ct);

        if (!result.Success)
        {
            var msg = $"Ошибка при записи значения 0x{ZeroValue:X4} в регистр {RegisterChPumpOverrun}. {result.Error}";
            logger.LogError(msg);
            return TestStepResult.Fail(msg);
        }

        logger.LogInformation("Время выбега насоса ОС сброшено успешно");
        return TestStepResult.Pass();
    }
}

using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Services.Steps.Steps.Coms;

/// <summary>
/// Тестовый шаг записи ступени вентилятора в ЭБУ котла.
/// Записывает значение 1 в регистр 1062 (2.b.d).
/// </summary>
public class SetFanMapStep(
    IOptions<DiagnosticSettings> settings,
    DualLogger<SetFanMapStep> logger) : ITestStep
{
    private const ushort RegisterFanMap = 1062;
    private const ushort FanStageValue = 1;

    private readonly DiagnosticSettings _settings = settings.Value;

    public string Id => "coms-set-fan-map";
    public string Name => "Coms/Set_Fan_Map";
    public string Description => "Запись ступени вентилятора";

    /// <summary>
    /// Записывает ступень вентилятора (значение 1) в регистр 1062.
    /// </summary>
    /// <param name="context">Контекст выполнения тестового шага.</param>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Запись ступени вентилятора {Value} в регистр {Register}",
            FanStageValue, RegisterFanMap);

        var modbusAddress = (ushort)(RegisterFanMap - _settings.BaseAddressOffset);
        var result = await context.DiagWriter.WriteUInt16Async(modbusAddress, FanStageValue, ct);

        if (!result.Success)
        {
            var msg = $"Ошибка при записи значения 0x{FanStageValue:X4} в регистр {RegisterFanMap}. {result.Error}";
            logger.LogError(msg);
            return TestStepResult.Fail(msg);
        }

        logger.LogInformation("Ступень вентилятора записана успешно");
        return TestStepResult.Pass();
    }
}

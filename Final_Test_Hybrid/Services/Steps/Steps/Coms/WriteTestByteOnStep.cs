using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Access;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.Coms;

/// <summary>
/// Тестовый шаг установки котла в режим "Стенд".
/// Записывает ключ доступа 0xD7F8DB56 в регистры 1000-1001.
/// </summary>
public class WriteTestByteOnStep(
    AccessLevelManager accessLevelManager,
    DualLogger<WriteTestByteOnStep> logger) : ITestStep
{
    public string Id => "coms-write-test-byte-on";
    public string Name => "Coms/Write_Test_Byte_ON";
    public string Description => "Установка котла в режим \"Стенд\"";

    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Установка режима Стенд");

        var result = await accessLevelManager.SetStandModeAsync(context.DiagWriter, ct);

        if (!result.Success)
        {
            // Детальное сообщение уже залогировано в AccessLevelManager
            return TestStepResult.Fail("Сбой при работе с котлом");
        }

        logger.LogInformation("Режим Стенд установлен успешно");
        return TestStepResult.Pass();
    }
}

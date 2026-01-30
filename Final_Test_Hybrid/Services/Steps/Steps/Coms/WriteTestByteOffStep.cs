using Final_Test_Hybrid.Models.Errors;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Access;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.Coms;

/// <summary>
/// Тестовый шаг вывода котла из режима "Стенд".
/// Записывает ключ сброса 0x00000000 в регистры 1000-1001 для перехода в обычный режим.
/// </summary>
public class WriteTestByteOffStep(
    AccessLevelManager accessLevelManager,
    DualLogger<WriteTestByteOffStep> logger) : ITestStep
{
    public string Id => "coms-write-test-byte-off";
    public string Name => "Coms/Write_Test_Byte_OFF";
    public string Description => "Вывод котла из режима \"Стенд\"";

    /// <summary>
    /// Выводит котёл из режима "Стенд", записывая ключ сброса в регистры доступа.
    /// </summary>
    /// <param name="context">Контекст выполнения шага.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Вывод котла из режима Стенд");

        var result = await accessLevelManager.ResetToNormalModeAsync(context.DiagWriter, ct);

        if (!result.Success)
        {
            var errorMsg = $"Ошибка при записи ключа сброса в регистры 1000-1001. {result.Error}";
            logger.LogError(errorMsg);
            return TestStepResult.Fail(errorMsg, errors: [ErrorDefinitions.WriteBytesOff]);
        }

        logger.LogInformation("Котёл выведен из режима Стенд");
        return TestStepResult.Pass();
    }
}

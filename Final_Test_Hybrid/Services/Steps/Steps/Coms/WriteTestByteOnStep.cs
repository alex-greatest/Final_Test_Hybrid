using Final_Test_Hybrid.Models.Errors;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Access;
using Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.Coms;

/// <summary>
/// Тестовый шаг установки котла в режим "Стенд".
/// Записывает ключ доступа 0xD7F8DB56 в регистры 1000-1001.
/// </summary>
public class WriteTestByteOnStep(
    AccessLevelManager accessLevelManager,
    IModbusDispatcher dispatcher,
    DualLogger<WriteTestByteOnStep> logger) : ITestStep
{
    public string Id => "coms-write-test-byte-on";
    public string Name => "Coms/Write_Test_Byte_ON";
    public string Description => "Установка котла в режим \"Стенд\"";

    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Установка режима Стенд");

        var result = await StandModeWriteExecutionHelper.ExecuteAsync(
            context,
            dispatcher,
            innerCt => accessLevelManager.SetStandModeAsync(context.PacedDiagWriter, innerCt),
            logger,
            ct);

        if (!result.Success)
        {
            var errorMsg = ComsStepFailureHelper.BuildWriteMessage(
                result,
                "записи ключа 0xD7F8DB56 в регистры 1000-1001",
                $"Ошибка при записи ключа 0xD7F8DB56 в регистры 1000-1001. {result.Error}");
            logger.LogError(errorMsg);
            return TestStepResult.Fail(errorMsg, errors: [ErrorDefinitions.WriteBytesOn]);
        }

        logger.LogInformation("Режим Стенд установлен успешно");
        return TestStepResult.Pass();
    }
}

using Final_Test_Hybrid.Models.Errors;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Access;
using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Services.Steps.Steps.Coms;

/// <summary>
/// Проверка режима "Стенд" котла.
/// При ошибке на Retry автоматически пытается установить режим заново.
/// Реализует INonSkippable — пропуск запрещён (режим обязателен для теста).
/// </summary>
public class CheckTestByteOnStep(
    AccessLevelManager accessLevelManager,
    IOptions<DiagnosticSettings> settings,
    DualLogger<CheckTestByteOnStep> logger) : ITestStep, INonSkippable
{
    private const string HadErrorKey = "coms-check-test-byte-on-had-error";
    private const ushort ModeKeyAddressDoc = 1000;
    private const uint StandModeKey = 0xD7F8_DB56;

    private readonly DiagnosticSettings _settings = settings.Value;

    public string Id => "coms-check-test-byte-on";
    public string Name => "Coms/Check_Test_Byte_ON";
    public string Description => "Проверка режима \"Стенд\" котла";

    /// <summary>
    /// Выполняет проверку режима Стенд котла.
    /// При retry пытается установить режим заново.
    /// </summary>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        var isRetry = context.Variables.ContainsKey(HadErrorKey);

        if (isRetry)
        {
            return await HandleRetryAsync(context, ct);
        }

        return await CheckStandModeAsync(context, ct);
    }

    /// <summary>
    /// Обрабатывает повторную попытку после ошибки.
    /// </summary>
    private async Task<TestStepResult> HandleRetryAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Retry: пытаемся установить режим Стенд");

        var setResult = await accessLevelManager.SetStandModeAsync(context.DiagWriter, ct);
        if (!setResult.Success)
        {
            return CreateWriteError(setResult.Error ?? "Неизвестная ошибка");
        }

        return await CheckStandModeAsync(context, ct);
    }

    /// <summary>
    /// Проверяет режим Стенд котла через чтение ModeKey.
    /// </summary>
    private async Task<TestStepResult> CheckStandModeAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Проверка режима Стенд котла");

        // Задержка для применения режима ECU (после WriteTestByteOnStep или SetStandModeAsync)
        await context.DelayAsync(TimeSpan.FromMilliseconds(_settings.WriteVerifyDelayMs), ct);

        var modbusAddress = (ushort)(ModeKeyAddressDoc - _settings.BaseAddressOffset);
        var readResult = await context.DiagReader.ReadUInt32Async(modbusAddress, ct);

        if (!readResult.Success)
        {
            context.Variables[HadErrorKey] = true;
            return CreateReadError(readResult.Error ?? "Неизвестная ошибка");
        }

        if (readResult.Value == StandModeKey)
        {
            logger.LogInformation("Котёл в режиме Стенд");
            context.Variables.Remove(HadErrorKey);
            return TestStepResult.Pass();
        }

        logger.LogWarning("Котёл НЕ в режиме Стенд (ModeKey: 0x{Key:X8})", readResult.Value);
        context.Variables[HadErrorKey] = true;
        return CreateModeError(readResult.Value);
    }

    /// <summary>
    /// Создаёт результат ошибки чтения.
    /// </summary>
    private TestStepResult CreateReadError(string error)
    {
        var msg = $"Ошибка при чтении ключа из регистров {ModeKeyAddressDoc}-{ModeKeyAddressDoc + 1}. {error}";
        logger.LogError(msg);
        return TestStepResult.Fail(msg, errors: [ErrorDefinitions.BoilerNotStandMode]);
    }

    /// <summary>
    /// Создаёт результат ошибки записи.
    /// </summary>
    private TestStepResult CreateWriteError(string error)
    {
        var msg = $"Ошибка при записи ключа 0x{StandModeKey:X8} в регистры {ModeKeyAddressDoc}-{ModeKeyAddressDoc + 1}. {error}";
        logger.LogError(msg);
        return TestStepResult.Fail(msg, errors: [ErrorDefinitions.BoilerNotStandMode]);
    }

    /// <summary>
    /// Создаёт результат ошибки неверного режима.
    /// </summary>
    private TestStepResult CreateModeError(uint actualKey)
    {
        var msg = $"Ошибка: прочитан ключ 0x{actualKey:X8} из регистров {ModeKeyAddressDoc}-{ModeKeyAddressDoc + 1}, ожидался 0x{StandModeKey:X8}";
        logger.LogError(msg);
        return TestStepResult.Fail(msg, errors: [ErrorDefinitions.BoilerNotStandMode]);
    }
}

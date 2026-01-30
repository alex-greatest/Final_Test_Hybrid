using Final_Test_Hybrid.Models.Errors;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Access;
using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Services.Steps.Steps.Coms;

/// <summary>
/// Проверка отключения режима "Стенд" котла.
/// При ошибке на Retry автоматически пытается сбросить режим заново.
/// Реализует INonSkippable — пропуск запрещён (выход из режима обязателен).
/// </summary>
public class CheckTestByteOffStep(
    AccessLevelManager accessLevelManager,
    IOptions<DiagnosticSettings> settings,
    DualLogger<CheckTestByteOffStep> logger) : ITestStep, INonSkippable
{
    private const string HadErrorKey = "coms-check-test-byte-off-had-error";
    private const ushort ModeKeyAddressDoc = 1000;
    private const uint StandModeKey = 0xD7F8_DB56;

    private readonly DiagnosticSettings _settings = settings.Value;

    public string Id => "coms-check-test-byte-off";
    public string Name => "Coms/Check_Test_Byte_OFF";
    public string Description => "Проверка отключения режима \"Стенд\" котла";

    /// <summary>
    /// Выполняет проверку выхода из режима Стенд котла.
    /// При retry пытается сбросить режим заново.
    /// </summary>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        var isRetry = context.Variables.ContainsKey(HadErrorKey);

        if (isRetry)
        {
            return await HandleRetryAsync(context, ct);
        }

        return await CheckNormalModeAsync(context, ct);
    }

    /// <summary>
    /// Обрабатывает повторную попытку после ошибки.
    /// </summary>
    private async Task<TestStepResult> HandleRetryAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Retry: пытаемся сбросить режим Стенд");

        var resetResult = await accessLevelManager.ResetToNormalModeAsync(context.DiagWriter, ct);
        if (!resetResult.Success)
        {
            return CreateWriteError(resetResult.Error ?? "Неизвестная ошибка");
        }

        return await CheckNormalModeAsync(context, ct);
    }

    /// <summary>
    /// Проверяет выход из режима Стенд котла через чтение ModeKey.
    /// </summary>
    private async Task<TestStepResult> CheckNormalModeAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Проверка выхода из режима Стенд котла");

        // Задержка для применения режима ECU (после WriteTestByteOffStep или ResetToNormalModeAsync)
        await context.DelayAsync(TimeSpan.FromMilliseconds(_settings.WriteVerifyDelayMs), ct);

        var modbusAddress = (ushort)(ModeKeyAddressDoc - _settings.BaseAddressOffset);
        var readResult = await context.DiagReader.ReadUInt32Async(modbusAddress, ct);

        if (!readResult.Success)
        {
            context.Variables[HadErrorKey] = true;
            return CreateReadError(readResult.Error ?? "Неизвестная ошибка");
        }

        if (readResult.Value != StandModeKey)
        {
            logger.LogInformation("Котёл вышел из режима Стенд (ModeKey: 0x{Key:X8})", readResult.Value);
            context.Variables.Remove(HadErrorKey);
            return TestStepResult.Pass();
        }

        logger.LogWarning("Котёл всё ещё в режиме Стенд (ModeKey: 0x{Key:X8})", readResult.Value);
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
        return TestStepResult.Fail(msg, errors: [ErrorDefinitions.BoilerStillInStandMode]);
    }

    /// <summary>
    /// Создаёт результат ошибки записи.
    /// </summary>
    private TestStepResult CreateWriteError(string error)
    {
        var msg = $"Ошибка при сбросе ключа в регистры {ModeKeyAddressDoc}-{ModeKeyAddressDoc + 1}. {error}";
        logger.LogError(msg);
        return TestStepResult.Fail(msg, errors: [ErrorDefinitions.BoilerStillInStandMode]);
    }

    /// <summary>
    /// Создаёт результат ошибки неверного режима.
    /// </summary>
    private TestStepResult CreateModeError(uint actualKey)
    {
        var msg = $"Ошибка: прочитан ключ 0x{actualKey:X8} из регистров {ModeKeyAddressDoc}-{ModeKeyAddressDoc + 1}, ожидался НЕ 0x{StandModeKey:X8}";
        logger.LogError(msg);
        return TestStepResult.Fail(msg, errors: [ErrorDefinitions.BoilerStillInStandMode]);
    }
}

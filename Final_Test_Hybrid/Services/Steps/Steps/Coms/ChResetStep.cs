using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Access;
using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Services.Steps.Steps.Coms;

/// <summary>
/// Перевод котла в летний режим (сброс отопления).
/// При ошибке на Retry автоматически пытается установить режим стенда заново.
/// Реализует INonSkippable — пропуск запрещён.
/// </summary>
public class ChResetStep(
    AccessLevelManager accessLevelManager,
    IOptions<DiagnosticSettings> settings,
    DualLogger<ChResetStep> logger) : ITestStep, INonSkippable
{
    private const string HadErrorKey = "coms-ch-reset-had-error";
    private const ushort RegisterChResetDoc = 1036;
    private const ushort ResetValue = 0;

    private readonly DiagnosticSettings _settings = settings.Value;

    public string Id => "coms-ch-reset";
    public string Name => "Coms/CH_Reset";
    public string Description => "Перевод котла в летний режим";

    /// <summary>
    /// Выполняет перевод котла в летний режим.
    /// При retry пытается установить режим стенда заново.
    /// </summary>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        var isRetry = context.Variables.ContainsKey(HadErrorKey);

        if (isRetry)
        {
            return await HandleRetryAsync(context, ct);
        }

        return await ResetHeatingModeAsync(context, ct);
    }

    /// <summary>
    /// Обрабатывает повторную попытку после ошибки.
    /// </summary>
    private async Task<TestStepResult> HandleRetryAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Retry: пытаемся установить режим Стенд перед сбросом отопления");

        var setResult = await accessLevelManager.SetStandModeAsync(context.DiagWriter, ct);
        if (!setResult.Success)
        {
            return CreateStandModeError(setResult.Error ?? "Неизвестная ошибка");
        }

        await context.DelayAsync(TimeSpan.FromMilliseconds(_settings.WriteVerifyDelayMs), ct);

        return await ResetHeatingModeAsync(context, ct);
    }

    /// <summary>
    /// Переводит котёл в летний режим записью 0 в регистр отопления.
    /// </summary>
    private async Task<TestStepResult> ResetHeatingModeAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Перевод котла в летний режим");

        var modbusAddress = (ushort)(RegisterChResetDoc - _settings.BaseAddressOffset);

        var writeResult = await context.DiagWriter.WriteUInt16Async(modbusAddress, ResetValue, ct);
        if (!writeResult.Success)
        {
            context.Variables[HadErrorKey] = true;
            return CreateWriteError(writeResult.Error ?? "Неизвестная ошибка");
        }

        await context.DelayAsync(TimeSpan.FromMilliseconds(_settings.WriteVerifyDelayMs), ct);

        var readResult = await context.DiagReader.ReadUInt16Async(modbusAddress, ct);
        if (!readResult.Success)
        {
            context.Variables[HadErrorKey] = true;
            return CreateReadError(readResult.Error ?? "Неизвестная ошибка");
        }

        if (readResult.Value == ResetValue)
        {
            logger.LogInformation("Котёл переведён в летний режим");
            context.Variables.Remove(HadErrorKey);
            return TestStepResult.Pass();
        }

        logger.LogWarning("Режим работы котла не изменён (прочитано: {Value}, ожидалось: {Expected})",
            readResult.Value, ResetValue);
        context.Variables[HadErrorKey] = true;
        return CreateModeError(readResult.Value);
    }

    /// <summary>
    /// Создаёт результат ошибки установки режима стенда.
    /// </summary>
    private TestStepResult CreateStandModeError(string error)
    {
        var msg = $"Ошибка установки режима Стенд. {error}";
        logger.LogError(msg);
        return TestStepResult.Fail(msg);
    }

    /// <summary>
    /// Создаёт результат ошибки записи.
    /// </summary>
    private TestStepResult CreateWriteError(string error)
    {
        var msg = $"Ошибка записи в регистр {RegisterChResetDoc}. {error}";
        logger.LogError(msg);
        return TestStepResult.Fail(msg);
    }

    /// <summary>
    /// Создаёт результат ошибки чтения.
    /// </summary>
    private TestStepResult CreateReadError(string error)
    {
        var msg = $"Ошибка чтения регистра {RegisterChResetDoc}. {error}";
        logger.LogError(msg);
        return TestStepResult.Fail(msg);
    }

    /// <summary>
    /// Создаёт результат ошибки неверного режима.
    /// </summary>
    private TestStepResult CreateModeError(ushort actualValue)
    {
        var msg = "Режим работы котла не изменен";
        logger.LogError("{Msg} (прочитано: {Value}, ожидалось: {Expected})", msg, actualValue, ResetValue);
        return TestStepResult.Fail(msg);
    }
}

using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Access;
using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Services.Steps.Steps.Coms;

/// <summary>
/// Переключение котла в режим БКН, включение насоса.
/// При ошибке на Retry автоматически пытается установить режим стенда заново.
/// Реализует INonSkippable — пропуск запрещён.
/// </summary>
public class SetDhwTankModeStep(
    AccessLevelManager accessLevelManager,
    IOptions<DiagnosticSettings> settings,
    DualLogger<SetDhwTankModeStep> logger) : ITestStep, INonSkippable
{
    private const string HadErrorKey = "coms-set-dhw-tank-mode-had-error";
    private const ushort RegisterTankModeDoc = 1189;
    private const ushort TankModeValue = 1;

    private readonly DiagnosticSettings _settings = settings.Value;

    public string Id => "coms-set-dhw-tank-mode";
    public string Name => "Coms/Set_DHW_Tank_Mode";
    public string Description => "Переключение котла в режим БКН, включение насоса";

    /// <summary>
    /// Выполняет переключение котла в режим БКН.
    /// При retry пытается установить режим стенда заново.
    /// </summary>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        var isRetry = context.Variables.ContainsKey(HadErrorKey);

        if (isRetry)
        {
            return await HandleRetryAsync(context, ct);
        }

        return await SetTankModeAsync(context, ct);
    }

    /// <summary>
    /// Обрабатывает повторную попытку после ошибки.
    /// </summary>
    private async Task<TestStepResult> HandleRetryAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Retry: пытаемся установить режим Стенд перед переключением в БКН");

        var setResult = await accessLevelManager.SetStandModeAsync(context.DiagWriter, ct);
        if (!setResult.Success)
        {
            return CreateStandModeError(setResult.Error ?? "Неизвестная ошибка");
        }

        await context.DelayAsync(TimeSpan.FromMilliseconds(_settings.WriteVerifyDelayMs), ct);

        return await SetTankModeAsync(context, ct);
    }

    /// <summary>
    /// Переключает котёл в режим БКН записью 1 в регистр режима.
    /// </summary>
    private async Task<TestStepResult> SetTankModeAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Переключение котла в режим БКН");

        var modbusAddress = (ushort)(RegisterTankModeDoc - _settings.BaseAddressOffset);

        var writeResult = await context.DiagWriter.WriteUInt16Async(modbusAddress, TankModeValue, ct);
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

        if (readResult.Value == TankModeValue)
        {
            logger.LogInformation("Котёл переключён в режим БКН, насос запущен");
            context.Variables.Remove(HadErrorKey);
            return TestStepResult.Pass();
        }

        logger.LogWarning("Режим работы котла не изменён (прочитано: {Value}, ожидалось: {Expected})",
            readResult.Value, TankModeValue);
        context.Variables[HadErrorKey] = true;
        return CreatePumpStartError(readResult.Value);
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
        var msg = $"Ошибка записи в регистр {RegisterTankModeDoc}. {error}";
        logger.LogError(msg);
        return TestStepResult.Fail(msg);
    }

    /// <summary>
    /// Создаёт результат ошибки чтения.
    /// </summary>
    private TestStepResult CreateReadError(string error)
    {
        var msg = $"Ошибка чтения регистра {RegisterTankModeDoc}. {error}";
        logger.LogError(msg);
        return TestStepResult.Fail(msg);
    }

    /// <summary>
    /// Создаёт результат ошибки запуска насоса.
    /// </summary>
    private TestStepResult CreatePumpStartError(ushort actualValue)
    {
        var msg = "Ошибка запуска насоса";
        logger.LogError("{Msg} (прочитано: {Value}, ожидалось: {Expected})", msg, actualValue, TankModeValue);
        return TestStepResult.Fail(msg);
    }
}

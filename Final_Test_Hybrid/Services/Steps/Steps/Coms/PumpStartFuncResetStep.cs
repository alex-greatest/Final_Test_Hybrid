using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Access;
using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Services.Steps.Steps.Coms;

/// <summary>
/// Сброс принудительного пуска насоса котла.
/// При ошибке на Retry автоматически пытается установить режим стенда заново.
/// Реализует INonSkippable — пропуск запрещён.
/// </summary>
public class PumpStartFuncResetStep(
    AccessLevelManager accessLevelManager,
    IOptions<DiagnosticSettings> settings,
    DualLogger<PumpStartFuncResetStep> logger) : ITestStep, INonSkippable
{
    private const string HadErrorKey = "coms-pump-start-func-reset-had-error";
    private const string ExecutedKey = "coms-pump-start-func-reset-executed";
    private const ushort RegisterPumpStartFuncDoc = 1189;
    private const ushort ResetValue = 0;

    private readonly DiagnosticSettings _settings = settings.Value;

    /// <inheritdoc />
    public string Id => "coms-pump-start-func-reset";

    /// <inheritdoc />
    public string Name => "Coms/Pump_Start_Func_Reset";

    /// <inheritdoc />
    public string Description => "Сброс пуска насоса котла";

    /// <summary>
    /// Выполняет сброс принудительного пуска насоса котла.
    /// При retry пытается установить режим стенда заново.
    /// </summary>
    /// <param name="context">Контекст выполнения шага.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        var isRetry = context.Variables.ContainsKey(HadErrorKey);

        if (isRetry)
        {
            return await HandleRetryAsync(context, ct);
        }

        return await ResetPumpStartFuncAsync(context, ct);
    }

    /// <summary>
    /// Обрабатывает повторную попытку после ошибки.
    /// </summary>
    private async Task<TestStepResult> HandleRetryAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Retry: пытаемся установить режим Стенд перед сбросом насоса");

        var setResult = await accessLevelManager.SetStandModeAsync(context.DiagWriter, ct);
        if (!setResult.Success)
        {
            return MarkExecutedAndReturn(context, CreateStandModeError(setResult.Error ?? "Неизвестная ошибка"));
        }

        await context.DelayAsync(TimeSpan.FromMilliseconds(_settings.WriteVerifyDelayMs), ct);

        return await ResetPumpStartFuncAsync(context, ct);
    }

    /// <summary>
    /// Сбрасывает принудительный пуск насоса записью 0 в регистр.
    /// </summary>
    private async Task<TestStepResult> ResetPumpStartFuncAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Сброс принудительного пуска насоса котла");

        return await WriteAndVerifyAsync(context, ct);
    }

    /// <summary>
    /// Записывает значение 0 в регистр и проверяет результат чтением.
    /// </summary>
    private async Task<TestStepResult> WriteAndVerifyAsync(TestStepContext context, CancellationToken ct)
    {
        var modbusAddress = (ushort)(RegisterPumpStartFuncDoc - _settings.BaseAddressOffset);

        var writeResult = await context.DiagWriter.WriteUInt16Async(modbusAddress, ResetValue, ct);
        if (!writeResult.Success)
        {
            context.Variables[HadErrorKey] = true;
            return MarkExecutedAndReturn(context, CreateWriteError(writeResult.Error ?? "Неизвестная ошибка"));
        }

        await context.DelayAsync(TimeSpan.FromMilliseconds(_settings.WriteVerifyDelayMs), ct);

        var readResult = await context.DiagReader.ReadUInt16Async(modbusAddress, ct);
        if (!readResult.Success)
        {
            context.Variables[HadErrorKey] = true;
            return MarkExecutedAndReturn(context, CreateReadError(readResult.Error ?? "Неизвестная ошибка"));
        }

        if (readResult.Value == ResetValue)
        {
            logger.LogInformation("Принудительный пуск насоса сброшен успешно");
            context.Variables.Remove(HadErrorKey);
            return MarkExecutedAndReturn(context, TestStepResult.Pass());
        }

        logger.LogWarning("Принудительный режим насоса не сброшен (прочитано: {Value}, ожидалось: {Expected})",
            readResult.Value, ResetValue);
        context.Variables[HadErrorKey] = true;
        return MarkExecutedAndReturn(context, CreateModeError(readResult.Value));
    }

    /// <summary>
    /// Помечает шаг как выполненный и возвращает результат.
    /// </summary>
    private static TestStepResult MarkExecutedAndReturn(TestStepContext context, TestStepResult result)
    {
        context.Variables[ExecutedKey] = true;
        return result;
    }

    /// <summary>
    /// Создаёт результат ошибки установки режима стенда.
    /// </summary>
    private TestStepResult CreateStandModeError(string error)
    {
        var msg = $"Ошибка при установке режима Стенд перед сбросом насоса. {error}";
        logger.LogError(msg);
        return TestStepResult.Fail(msg);
    }

    /// <summary>
    /// Создаёт результат ошибки записи.
    /// </summary>
    private TestStepResult CreateWriteError(string error)
    {
        var msg = $"Ошибка при записи значения 0x{ResetValue:X4} в регистр {RegisterPumpStartFuncDoc}. {error}";
        logger.LogError(msg);
        return TestStepResult.Fail(msg);
    }

    /// <summary>
    /// Создаёт результат ошибки чтения.
    /// </summary>
    private TestStepResult CreateReadError(string error)
    {
        var msg = $"Ошибка при чтении регистра {RegisterPumpStartFuncDoc}. {error}";
        logger.LogError(msg);
        return TestStepResult.Fail(msg);
    }

    /// <summary>
    /// Создаёт результат ошибки неверного значения.
    /// </summary>
    private TestStepResult CreateModeError(ushort actualValue)
    {
        var msg = $"Ошибка: прочитано значение 0x{actualValue:X4} из регистра {RegisterPumpStartFuncDoc}, ожидалось 0x{ResetValue:X4}";
        logger.LogError(msg);
        return TestStepResult.Fail(msg);
    }
}

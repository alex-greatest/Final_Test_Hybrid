using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Access;
using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Final_Test_Hybrid.Services.Diagnostic.Models;
using Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;
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
    IModbusDispatcher dispatcher,
    IOptions<DiagnosticSettings> settings,
    DualLogger<PumpStartFuncResetStep> logger) : ITestStep, INonSkippable
{
    private const string HadErrorKey = "coms-pump-start-func-reset-had-error";
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

        var setResult = await StandModeWriteExecutionHelper.ExecuteAsync(
            context,
            dispatcher,
            innerCt => accessLevelManager.SetStandModeAsync(context.PacedDiagWriter, innerCt),
            logger,
            ct);
        if (!setResult.Success)
        {
            return CreateStandModeError(setResult);
        }

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

        var writeResult = await context.PacedDiagWriter.WriteUInt16Async(modbusAddress, ResetValue, ct);
        if (!writeResult.Success)
        {
            context.Variables[HadErrorKey] = true;
            return CreateWriteError(writeResult);
        }

        var readResult = await context.PacedDiagReader.ReadUInt16Async(modbusAddress, ct);
        if (!readResult.Success)
        {
            context.Variables[HadErrorKey] = true;
            return CreateReadError(readResult);
        }

        if (readResult.Value == ResetValue)
        {
            logger.LogInformation("Принудительный пуск насоса сброшен успешно");
            context.Variables.Remove(HadErrorKey);
            return TestStepResult.Pass();
        }

        logger.LogWarning("Принудительный режим насоса не сброшен (прочитано: {Value}, ожидалось: {Expected})",
            readResult.Value, ResetValue);
        context.Variables[HadErrorKey] = true;
        return CreateModeError(readResult.Value);
    }

    /// <summary>
    /// Создаёт результат ошибки установки режима стенда.
    /// </summary>
    private TestStepResult CreateStandModeError(DiagnosticWriteResult result)
    {
        var msg = ComsStepFailureHelper.BuildWriteMessage(result, "установке режима Стенд перед сбросом насоса", $"Ошибка при установке режима Стенд перед сбросом насоса. {result.Error ?? "Неизвестная ошибка"}");
        logger.LogError(msg);
        return TestStepResult.Fail(msg);
    }

    /// <summary>
    /// Создаёт результат ошибки записи.
    /// </summary>
    private TestStepResult CreateWriteError(DiagnosticWriteResult result)
    {
        var msg = ComsStepFailureHelper.BuildWriteMessage(result, $"записи значения 0x{ResetValue:X4} в регистр {RegisterPumpStartFuncDoc}", $"Ошибка при записи значения 0x{ResetValue:X4} в регистр {RegisterPumpStartFuncDoc}. {result.Error ?? "Неизвестная ошибка"}");
        logger.LogError(msg);
        return TestStepResult.Fail(msg);
    }

    /// <summary>
    /// Создаёт результат ошибки чтения.
    /// </summary>
    private TestStepResult CreateReadError(DiagnosticReadResult<ushort> result)
    {
        var msg = ComsStepFailureHelper.BuildReadMessage(result, $"чтении регистра {RegisterPumpStartFuncDoc}", $"Ошибка при чтении регистра {RegisterPumpStartFuncDoc}. {result.Error ?? "Неизвестная ошибка"}");
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

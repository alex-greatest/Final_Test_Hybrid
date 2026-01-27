using Final_Test_Hybrid.Models.Errors;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Access;
using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Services.Steps.Steps.Coms;

/// <summary>
/// Запуск насоса котла в режиме отопления.
/// При ошибке на Retry автоматически пытается установить режим стенда заново.
/// Реализует INonSkippable — пропуск запрещён.
/// </summary>
public class ChPumpStartStep(
    AccessLevelManager accessLevelManager,
    IOptions<DiagnosticSettings> settings,
    DualLogger<ChPumpStartStep> logger) : ITestStep, INonSkippable
{
    private const string HadErrorKey = "coms-ch-pump-start-had-error";
    private const ushort TestModeRegisterDoc = 1189;
    private const ushort HeatingModeWithPump = 2;

    private readonly DiagnosticSettings _settings = settings.Value;

    public string Id => "coms-ch-pump-start";
    public string Name => "Coms/CH_Pump_Start";
    public string Description => "Запуск насоса котла";

    /// <summary>
    /// Выполняет запуск насоса котла в режиме отопления.
    /// При retry пытается установить режим стенда заново.
    /// </summary>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        var isRetry = context.Variables.ContainsKey(HadErrorKey);

        if (isRetry)
        {
            return await HandleRetryAsync(context, ct);
        }

        return await StartPumpAsync(context, ct);
    }

    /// <summary>
    /// Обрабатывает повторную попытку после ошибки.
    /// </summary>
    private async Task<TestStepResult> HandleRetryAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Retry: пытаемся установить режим Стенд перед запуском насоса");

        var setResult = await accessLevelManager.SetStandModeAsync(context.DiagWriter, ct);
        if (!setResult.Success)
        {
            return CreateStandModeError(setResult.Error ?? "Неизвестная ошибка");
        }

        await context.DelayAsync(TimeSpan.FromMilliseconds(_settings.WriteVerifyDelayMs), ct);

        return await StartPumpAsync(context, ct);
    }

    /// <summary>
    /// Запускает насос котла записью значения 2 в регистр тестового режима.
    /// </summary>
    private async Task<TestStepResult> StartPumpAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Запуск насоса котла в режиме отопления");

        return await WriteAndVerifyAsync(context, ct);
    }

    /// <summary>
    /// Записывает значение в регистр и проверяет результат чтением.
    /// </summary>
    private async Task<TestStepResult> WriteAndVerifyAsync(TestStepContext context, CancellationToken ct)
    {
        var modbusAddress = (ushort)(TestModeRegisterDoc - _settings.BaseAddressOffset);

        var writeResult = await context.DiagWriter.WriteUInt16Async(modbusAddress, HeatingModeWithPump, ct);
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

        if (readResult.Value == HeatingModeWithPump)
        {
            logger.LogInformation("Насос котла запущен успешно (режим отопления)");
            context.Variables.Remove(HadErrorKey);
            return TestStepResult.Pass();
        }

        logger.LogWarning("Режим тестирования не установлен (прочитано: {Value}, ожидалось: {Expected})",
            readResult.Value, HeatingModeWithPump);
        context.Variables[HadErrorKey] = true;
        return CreateModeError(readResult.Value);
    }

    /// <summary>
    /// Создаёт результат ошибки установки режима стенда.
    /// </summary>
    private TestStepResult CreateStandModeError(string error)
    {
        var msg = $"Ошибка при установке режима Стенд перед запуском насоса. {error}";
        logger.LogError(msg);
        return TestStepResult.Fail(msg, errors: [ErrorDefinitions.ChPumpStartError]);
    }

    /// <summary>
    /// Создаёт результат ошибки записи.
    /// </summary>
    private TestStepResult CreateWriteError(string error)
    {
        var msg = $"Ошибка при записи значения {HeatingModeWithPump} в регистр {TestModeRegisterDoc}. {error}";
        logger.LogError(msg);
        return TestStepResult.Fail(msg, errors: [ErrorDefinitions.ChPumpStartError]);
    }

    /// <summary>
    /// Создаёт результат ошибки чтения.
    /// </summary>
    private TestStepResult CreateReadError(string error)
    {
        var msg = $"Ошибка при чтении регистра {TestModeRegisterDoc}. {error}";
        logger.LogError(msg);
        return TestStepResult.Fail(msg, errors: [ErrorDefinitions.ChPumpStartError]);
    }

    /// <summary>
    /// Создаёт результат ошибки неверного режима.
    /// </summary>
    private TestStepResult CreateModeError(ushort actualValue)
    {
        var msg = $"Ошибка: прочитано значение {actualValue} из регистра {TestModeRegisterDoc}, ожидалось {HeatingModeWithPump}";
        logger.LogError(msg);
        return TestStepResult.Fail(msg, errors: [ErrorDefinitions.ChPumpStartError]);
    }
}

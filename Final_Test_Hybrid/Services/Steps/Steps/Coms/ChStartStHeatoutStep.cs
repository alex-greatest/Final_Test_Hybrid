using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Services.Steps.Steps.Coms;

/// <summary>
/// Тестовый шаг запуска нагрева котла на максимальной мощности (режим стенда).
/// Записывает значение 4 в регистр 1036 и проверяет переключение режима.
/// </summary>
/// <remarks>
/// Регистр 1036 (1.2.F) - "Кратковременная смена режима работы системы":
/// 0 – нормальный режим, 2 – настраиваемый, 3 – мин, 4 – макс.
/// </remarks>
public class ChStartStHeatoutStep(
    IOptions<DiagnosticSettings> settings,
    DualLogger<ChStartStHeatoutStep> logger) : ITestStep
{
    private const ushort RegisterOperationMode = 1036;
    private const ushort MaxHeatingMode = 4;

    private readonly DiagnosticSettings _settings = settings.Value;

    public string Id => "coms-ch-start-st-heatout";
    public string Name => "Coms/CH_Start_ST_Heatout";
    public string Description => "Запуск нагрева котла в нормальном режиме";

    /// <summary>
    /// Выполняет шаг запуска максимального нагрева.
    /// </summary>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Запуск шага нагрева котла в режиме максимальной мощности");

        var writeResult = await WriteOperationModeAsync(context, ct);
        if (!writeResult.Success)
        {
            return TestStepResult.Fail($"Ошибка записи режима в регистр {RegisterOperationMode}. {writeResult.Error}");
        }

        context.ReportProgress("Ожидание переключения режима...");
        await context.DelayAsync(TimeSpan.FromMilliseconds(_settings.WriteVerifyDelayMs), ct);

        return await VerifyOperationModeAsync(context, ct);
    }

    /// <summary>
    /// Записывает режим максимального нагрева (4) в регистр.
    /// </summary>
    private async Task<WriteOperationResult> WriteOperationModeAsync(TestStepContext context, CancellationToken ct)
    {
        var address = (ushort)(RegisterOperationMode - _settings.BaseAddressOffset);
        var result = await context.DiagWriter.WriteUInt16Async(address, MaxHeatingMode, ct);

        if (!result.Success)
        {
            logger.LogError("Ошибка записи режима {Mode} в регистр {Register}: {Error}",
                MaxHeatingMode, RegisterOperationMode, result.Error);
            return new WriteOperationResult(false, result.Error);
        }

        logger.LogInformation("Режим максимального нагрева ({Mode}) записан в регистр {Register}",
            MaxHeatingMode, RegisterOperationMode);
        return new WriteOperationResult(true, null);
    }

    /// <summary>
    /// Проверяет, что режим переключился на максимальный нагрев.
    /// </summary>
    private async Task<TestStepResult> VerifyOperationModeAsync(TestStepContext context, CancellationToken ct)
    {
        var address = (ushort)(RegisterOperationMode - _settings.BaseAddressOffset);
        var readResult = await context.DiagReader.ReadUInt16Async(address, ct);

        if (!readResult.Success)
        {
            logger.LogError("Ошибка чтения регистра {Register}: {Error}", RegisterOperationMode, readResult.Error);
            return TestStepResult.Fail($"Ошибка чтения регистра {RegisterOperationMode}. {readResult.Error}");
        }

        var actualValue = readResult.Value;

        if (actualValue == MaxHeatingMode)
        {
            logger.LogInformation("Режим переключен успешно. Регистр {Register} = {Value}",
                RegisterOperationMode, actualValue);
            return TestStepResult.Pass();
        }

        logger.LogWarning("Режим не переключен. Ожидалось {Expected}, прочитано {Actual}",
            MaxHeatingMode, actualValue);
        return TestStepResult.Fail($"Режим не переключен. Ожидалось {MaxHeatingMode}, прочитано {actualValue}");
    }

    /// <summary>
    /// Результат операции записи.
    /// </summary>
    private sealed record WriteOperationResult(bool Success, string? Error);
}

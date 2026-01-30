using System.Diagnostics;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Final_Test_Hybrid.Services.Results;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Limits;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Services.Steps.Steps.Coms;

/// <summary>
/// Тестовый шаг измерения времени безопасного отключения котла (safety time).
/// Измеряет время от момента включения катушек EV1/EV2 до их выключения.
/// </summary>
public class SafetyTimeStep(
    IOptions<DiagnosticSettings> settings,
    ITestResultsService testResultsService,
    DualLogger<SafetyTimeStep> logger) : ITestStep, IRequiresRecipes, IProvideLimits
{
    private const ushort RegisterEv1Current = 1023;
    private const ushort RegisterEv2Current = 1028;
    private const ushort RegisterBoilerStatus = 1005;
    private const ushort RegisterResetBlockage = 1153;
    private const ushort CurrentThresholdMa = 200;
    private const short BlockageBStatus = 1;
    private const ushort ResetValue = 0;
    private const int PollingIntervalMs = 100;
    private const int WaitTimeoutMs = 30_000;

    private const string SafetyTimeMinRecipe = "ns=3;s=\"DB_Recipe\".\"Time\".\"ignSafetyTimeMin\"";
    private const string SafetyTimeMaxRecipe = "ns=3;s=\"DB_Recipe\".\"Time\".\"ignSafetyTimeMax\"";
    private const string ResultParameterName = "Safety time";

    private readonly DiagnosticSettings _settings = settings.Value;

    public string Id => "coms-safety-time";
    public string Name => "Coms/Safety_Time";
    public string Description => "Измерение времени безопасного отключения котла";

    public IReadOnlyList<string> RequiredRecipeAddresses => [SafetyTimeMinRecipe, SafetyTimeMaxRecipe];

    /// <summary>
    /// Возвращает пределы для отображения в гриде.
    /// </summary>
    public string? GetLimits(LimitsContext context)
    {
        var min = context.RecipeProvider.GetValue<float>(SafetyTimeMinRecipe);
        var max = context.RecipeProvider.GetValue<float>(SafetyTimeMaxRecipe);
        return (min, max) switch
        {
            (not null, not null) => $"[{min:F2} .. {max:F2}] сек",
            _ => null
        };
    }

    /// <summary>
    /// Выполняет измерение времени безопасного отключения.
    /// </summary>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Запуск измерения времени безопасного отключения");

        RemoveOldResult();

        var coilsOnResult = await WaitForCoilsOnAsync(context, ct);
        if (coilsOnResult != null)
        {
            return coilsOnResult;
        }

        var safetyTimeResult = await MeasureSafetyTimeAsync(context, ct);
        if (!safetyTimeResult.Success)
        {
            return TestStepResult.Fail(safetyTimeResult.Error ?? "Ошибка измерения safety time");
        }

        var saveResult = SaveResult(context, safetyTimeResult.SafetyTime);
        if (saveResult != null)
        {
            return saveResult;
        }

        var resetResult = await CheckAndResetBlockageAsync(context, ct);
        if (resetResult != null)
        {
            return resetResult;
        }

        logger.LogInformation("Измерение safety time завершено: {SafetyTime:F2} сек", safetyTimeResult.SafetyTime);
        return TestStepResult.Pass($"{safetyTimeResult.SafetyTime:F2} сек");
    }

    /// <summary>
    /// Удаляет старый результат для корректной работы Retry.
    /// </summary>
    private void RemoveOldResult()
    {
        testResultsService.Remove(ResultParameterName);
    }

    /// <summary>
    /// Фаза 1: Ожидание включения катушек EV1/EV2 (ток >= 200 мА).
    /// </summary>
    private async Task<TestStepResult?> WaitForCoilsOnAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Ожидание включения катушек EV1/EV2...");
        context.ReportProgress("Ожидание включения катушек...");

        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.ElapsedMilliseconds < WaitTimeoutMs)
        {
            ct.ThrowIfCancellationRequested();

            var readResult = await ReadCoilCurrentsAsync(context, ct);
            if (!readResult.Success)
            {
                return TestStepResult.Fail($"Ошибка чтения тока катушек: {readResult.Error}");
            }

            if (readResult is { Ev1Current: >= CurrentThresholdMa, Ev2Current: >= CurrentThresholdMa })
            {
                logger.LogInformation("Катушки включены: EV1={Ev1} мА, EV2={Ev2} мА",
                    readResult.Ev1Current, readResult.Ev2Current);
                return null;
            }

            context.ReportProgress($"Ожидание катушек: EV1={readResult.Ev1Current} мА, EV2={readResult.Ev2Current} мА");
            await context.DelayAsync(TimeSpan.FromMilliseconds(PollingIntervalMs), ct);
        }

        return TestStepResult.Fail("Таймаут ожидания включения катушек (30 сек)");
    }

    /// <summary>
    /// Фаза 2: Измерение времени безопасного отключения.
    /// </summary>
    private async Task<SafetyTimeResult> MeasureSafetyTimeAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Измерение времени отключения катушек...");
        context.ReportProgress("Измерение времени отключения...");

        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.ElapsedMilliseconds < WaitTimeoutMs)
        {
            ct.ThrowIfCancellationRequested();

            var readResult = await ReadCoilCurrentsAsync(context, ct);
            if (!readResult.Success)
            {
                return new SafetyTimeResult(false, 0f, $"Ошибка чтения тока катушек: {readResult.Error}");
            }

            if (readResult.Ev1Current < CurrentThresholdMa && readResult.Ev2Current < CurrentThresholdMa)
            {
                stopwatch.Stop();
                var safetyTime = (float)(stopwatch.ElapsedMilliseconds / 1000.0);
                logger.LogInformation("Катушки выключены за {SafetyTime:F2} сек: EV1={Ev1} мА, EV2={Ev2} мА",
                    safetyTime, readResult.Ev1Current, readResult.Ev2Current);
                return new SafetyTimeResult(true, safetyTime, null);
            }

            context.ReportProgress($"Измерение: {stopwatch.ElapsedMilliseconds / 1000.0:F1} сек...");
            await context.DelayAsync(TimeSpan.FromMilliseconds(PollingIntervalMs), ct);
        }

        return new SafetyTimeResult(false, 0f, "Таймаут ожидания отключения катушек (30 сек)");
    }

    /// <summary>
    /// Фаза 3: Сохранение результата в TestResultService.
    /// </summary>
    private TestStepResult? SaveResult(TestStepContext context, float safetyTime)
    {
        var minValue = context.RecipeProvider.GetValue<float>(SafetyTimeMinRecipe);
        var maxValue = context.RecipeProvider.GetValue<float>(SafetyTimeMaxRecipe);

        if (minValue == null || maxValue == null)
        {
            return TestStepResult.Fail("Рецепты ignSafetyTimeMin/Max не загружены");
        }

        var min = minValue.Value;
        var max = maxValue.Value;
        var isInRange = safetyTime >= min && safetyTime <= max;
        var status = isInRange ? 1 : 2;

        testResultsService.Add(
            parameterName: ResultParameterName,
            value: $"{safetyTime:F2}",
            min: $"{min:F2}",
            max: $"{max:F2}",
            status: status,
            isRanged: true,
            unit: "сек");

        logger.LogInformation("Safety time: {SafetyTime:F2} сек, пределы: [{Min:F2} .. {Max:F2}], статус: {Status}",
            safetyTime, min, max, isInRange ? "OK" : "NOK");

        if (!isInRange)
        {
            return TestStepResult.Fail($"Safety time {safetyTime:F2} сек вне пределов [{min:F2} .. {max:F2}]");
        }

        return null;
    }

    /// <summary>
    /// Фаза 4: Проверка статуса котла и сброс блокировки Б.
    /// </summary>
    private async Task<TestStepResult?> CheckAndResetBlockageAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Проверка статуса котла...");

        var statusAddress = (ushort)(RegisterBoilerStatus - _settings.BaseAddressOffset);
        var statusResult = await context.DiagReader.ReadInt16Async(statusAddress, ct);

        if (!statusResult.Success)
        {
            return TestStepResult.Fail($"Ошибка чтения статуса котла: {statusResult.Error}");
        }

        if (statusResult.Value != BlockageBStatus)
        {
            logger.LogInformation("Статус котла: {Status} (не Блокировка Б)", statusResult.Value);
            return null;
        }

        logger.LogInformation("Статус котла: Блокировка Б, сбрасываем ошибку...");
        context.ReportProgress("Сброс блокировки...");

        var resetAddress = (ushort)(RegisterResetBlockage - _settings.BaseAddressOffset);
        var writeResult = await context.DiagWriter.WriteUInt16Async(resetAddress, ResetValue, ct);

        if (!writeResult.Success)
        {
            return TestStepResult.Fail($"Ошибка сброса блокировки: {writeResult.Error}");
        }

        await context.DelayAsync(TimeSpan.FromMilliseconds(_settings.WriteVerifyDelayMs), ct);
        logger.LogInformation("Блокировка Б сброшена");

        return null;
    }

    /// <summary>
    /// Читает токи катушек EV1 и EV2.
    /// </summary>
    private async Task<CoilCurrentsResult> ReadCoilCurrentsAsync(TestStepContext context, CancellationToken ct)
    {
        var ev1Address = (ushort)(RegisterEv1Current - _settings.BaseAddressOffset);
        var ev1Result = await context.DiagReader.ReadUInt16Async(ev1Address, ct);

        if (!ev1Result.Success)
        {
            return new CoilCurrentsResult(false, 0, 0, ev1Result.Error);
        }

        var ev2Address = (ushort)(RegisterEv2Current - _settings.BaseAddressOffset);
        var ev2Result = await context.DiagReader.ReadUInt16Async(ev2Address, ct);

        if (!ev2Result.Success)
        {
            return new CoilCurrentsResult(false, 0, 0, ev2Result.Error);
        }

        return new CoilCurrentsResult(true, ev1Result.Value, ev2Result.Value, null);
    }

    /// <summary>
    /// Результат чтения токов катушек.
    /// </summary>
    private sealed record CoilCurrentsResult(bool Success, ushort Ev1Current, ushort Ev2Current, string? Error);

    /// <summary>
    /// Результат измерения safety time.
    /// </summary>
    private sealed record SafetyTimeResult(bool Success, float SafetyTime, string? Error);
}

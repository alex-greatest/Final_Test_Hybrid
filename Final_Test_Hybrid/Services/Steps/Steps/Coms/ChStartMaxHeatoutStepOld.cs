using System.Diagnostics;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Access;
using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Limits;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Services.Steps.Steps.Coms;

/// <summary>
/// OLD-версия шага максимального нагрева.
/// Хранится для быстрого отката и не должна участвовать в runtime-регистрации шагов.
/// </summary>
/// <remarks>
/// Ошибки PLC (AlNoWaterFlow, AlIonCurrentOutTol) поднимаются самим PLC через ErrorTag.
/// В TestStepResult.Fail() передаются только программные ошибки, не связанные с PLC.
/// </remarks>
public class ChStartMaxHeatoutStepOld(
    AccessLevelManager accessLevelManager,
    IOptions<DiagnosticSettings> settings,
    DualLogger<ChStartMaxHeatoutStepOld> logger) : IHasPlcBlockPath, IRequiresPlcSubscriptions, IProvideLimits, INonSkippable
{
    private const string BlockPath = "DB_VI.Coms.CH_Start_Max_Heatout";
    private const string StartTag = "ns=3;s=\"DB_VI\".\"Coms\".\"CH_Start_Max_Heatout\".\"Start\"";
    private const string EndTag = "ns=3;s=\"DB_VI\".\"Coms\".\"CH_Start_Max_Heatout\".\"End\"";
    private const string ErrorTag = "ns=3;s=\"DB_VI\".\"Coms\".\"CH_Start_Max_Heatout\".\"Error\"";
    private const string Ready1Tag = "ns=3;s=\"DB_VI\".\"Coms\".\"CH_Start_Max_Heatout\".\"Ready_1\"";
    private const string Ready2Tag = "ns=3;s=\"DB_VI\".\"Coms\".\"CH_Start_Max_Heatout\".\"Ready_2\"";
    private const string Continua1Tag = "ns=3;s=\"DB_VI\".\"Coms\".\"CH_Start_Max_Heatout\".\"Сontinua_1\"";
    private const string Continua2Tag = "ns=3;s=\"DB_VI\".\"Coms\".\"CH_Start_Max_Heatout\".\"Сontinua_2\"";
    private const string FaultTag = "ns=3;s=\"DB_VI\".\"Coms\".\"CH_Start_Max_Heatout\".\"Fault\"";

    private const string HadErrorKey = "coms-ch-start-max-heatout-had-error";

    private const ushort RegisterOperationMode = 1036;
    private const ushort MaxHeatingMode = 4;
    private const ushort RegisterFlameIonization = 1014;
    private const float IonizationDivisor = 1000.0f;
    private const float IonizationMin = 1.0f;
    private const float IonizationMax = 15.0f;
    private const int NoIgnitionTimeoutMs = 10_000;
    private const int IonizationCheckIntervalMs = 400;
    private const int PlcCheckTimeoutMs = 100;

    private readonly DiagnosticSettings _settings = settings.Value;

    public string Id => "coms-ch-start-max-heatout";
    public string Name => "Coms/CH_Start_Max_Heatout";
    public string Description => "Запуск максимального нагрева контура отопления";
    public string PlcBlockPath => BlockPath;

    public IReadOnlyList<string> RequiredPlcTags =>
        [StartTag, EndTag, ErrorTag, Ready1Tag, Ready2Tag, Continua1Tag, Continua2Tag, FaultTag];

    /// <summary>
    /// Возвращает пределы для отображения в гриде.
    /// </summary>
    public string? GetLimits(LimitsContext context)
    {
        return $"[{IonizationMin:F0} .. {IonizationMax:F0}] µA";
    }

    /// <summary>
    /// Выполняет шаг запуска максимального нагрева.
    /// При retry пытается установить режим стенда заново.
    /// </summary>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        var isRetry = context.Variables.ContainsKey(HadErrorKey);

        if (isRetry)
        {
            return await HandleRetryAsync(context, ct);
        }

        return await StartMaxHeatoutAsync(context, ct);
    }

    /// <summary>
    /// Обрабатывает повторную попытку после ошибки.
    /// </summary>
    private async Task<TestStepResult> HandleRetryAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Retry: устанавливаем режим Стенд перед запуском максимального нагрева");

        var setResult = await accessLevelManager.SetStandModeAsync(context.DiagWriter, ct);
        if (!setResult.Success)
        {
            var msg = $"Ошибка установки режима Стенд. {setResult.Error}";
            logger.LogError(msg);
            return TestStepResult.Fail(msg);
        }

        await context.DelayAsync(TimeSpan.FromMilliseconds(_settings.WriteVerifyDelayMs), ct);

        return await StartMaxHeatoutAsync(context, ct);
    }

    /// <summary>
    /// Выполняет запуск максимального нагрева.
    /// </summary>
    private async Task<TestStepResult> StartMaxHeatoutAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Запуск шага максимального нагрева контура отопления");

        var writeResult = await context.OpcUa.WriteAsync(StartTag, true, ct);
        if (writeResult.Error != null)
        {
            return TestStepResult.Fail($"Ошибка записи Start: {writeResult.Error}");
        }

        return await WaitPhase1Async(context, ct);
    }

    /// <summary>
    /// Фаза 1: Ожидание End/Error/Ready_1.
    /// </summary>
    private async Task<TestStepResult> WaitPhase1Async(TestStepContext context, CancellationToken ct)
    {
        var result = await context.TagWaiter.WaitAnyAsync(
            context.TagWaiter.CreateWaitGroup<Phase1Result>()
                .WaitForTrue(EndTag, () => Phase1Result.End, "End")
                .WaitForTrue(ErrorTag, () => Phase1Result.Error, "Error")
                .WaitForTrue(Ready1Tag, () => Phase1Result.Ready1, "Ready_1"),
            ct);

        return result.Result switch
        {
            Phase1Result.End => await HandleSuccessAsync(context, null, ct),
            Phase1Result.Error => TestStepResult.Fail("Ошибка от PLC (фаза 1)"),
            Phase1Result.Ready1 => await HandleReady1Async(context, ct),
            _ => TestStepResult.Fail("Неизвестный результат фазы 1")
        };
    }

    /// <summary>
    /// Обрабатывает Ready_1: переключает котёл в режим максимального нагрева.
    /// </summary>
    private async Task<TestStepResult> HandleReady1Async(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Ready_1 получен, переключаем котёл в режим максимального нагрева");

        var address = (ushort)(RegisterOperationMode - _settings.BaseAddressOffset);
        var writeResult = await context.DiagWriter.WriteUInt16Async(address, MaxHeatingMode, ct);

        if (!writeResult.Success)
        {
            var msg = $"Ошибка записи режима в регистр {RegisterOperationMode}. {writeResult.Error}";
            logger.LogError(msg);
            await WriteFaultAsync(context, ct);
            return await WaitForEndOrErrorAsync(context, msg, ct);
        }

        // Верификация записи режима
        await context.DelayAsync(TimeSpan.FromMilliseconds(_settings.WriteVerifyDelayMs), ct);

        var readResult = await context.DiagReader.ReadUInt16Async(address, ct);
        if (!readResult.Success)
        {
            var msg = $"Ошибка чтения регистра {RegisterOperationMode}. {readResult.Error}";
            logger.LogError(msg);
            await WriteFaultAsync(context, ct);
            return await WaitForEndOrErrorAsync(context, msg, ct);
        }

        if (readResult.Value != MaxHeatingMode)
        {
            context.Variables[HadErrorKey] = true;
            var msg = $"Режим не установлен (прочитано: {readResult.Value}, ожидалось: {MaxHeatingMode})";
            logger.LogWarning(msg);
            await WriteFaultAsync(context, ct);
            return await WaitForEndOrErrorAsync(context, msg, ct);
        }

        context.ReportProgress("Команда максимального нагрева отправлена успешно");
        logger.LogInformation("Режим максимального нагрева (4) записан и верифицирован");

        var continua = await context.OpcUa.WriteAsync(Continua1Tag, true, ct);
        if (continua.Error != null)
        {
            await WriteFaultAsync(context, ct);
            return await WaitForEndOrErrorAsync(context, $"Ошибка записи Continua_1: {continua.Error}", ct);
        }

        return await WaitPhase2Async(context, ct);
    }

    /// <summary>
    /// Фаза 2: Ожидание End/Error/Ready_2.
    /// </summary>
    private async Task<TestStepResult> WaitPhase2Async(TestStepContext context, CancellationToken ct)
    {
        var result = await context.TagWaiter.WaitAnyAsync(
            context.TagWaiter.CreateWaitGroup<Phase2Result>()
                .WaitForTrue(EndTag, () => Phase2Result.End, "End")
                .WaitForTrue(ErrorTag, () => Phase2Result.Error, "Error")
                .WaitForTrue(Ready2Tag, () => Phase2Result.Ready2, "Ready_2"),
            ct);

        return result.Result switch
        {
            Phase2Result.End => await HandleSuccessAsync(context, null, ct),
            Phase2Result.Error => TestStepResult.Fail("Ошибка от PLC (фаза 2)"),
            Phase2Result.Ready2 => await HandleReady2Async(context, ct),
            _ => TestStepResult.Fail("Неизвестный результат фазы 2")
        };
    }

    /// <summary>
    /// Обрабатывает Ready_2: проверяет ток ионизации с параллельным контролем End/Error.
    /// </summary>
    private async Task<TestStepResult> HandleReady2Async(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Ready_2 получен, проверяем ток ионизации");
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.ElapsedMilliseconds < NoIgnitionTimeoutMs)
        {
            ct.ThrowIfCancellationRequested();

            var plcCheck = await CheckEndOrErrorAsync(context, TimeSpan.FromMilliseconds(PlcCheckTimeoutMs), ct);
            if (plcCheck != null)
            {
                return plcCheck;
            }

            var ionResult = await ReadIonizationCurrentAsync(context, ct);
            if (!ionResult.Success)
            {
                await WriteFaultAsync(context, ct);
                return await WaitPhase3WithErrorAsync(context,
                    $"Ошибка чтения тока ионизации: {ionResult.Error}", ct);
            }

            var ionization = ionResult.Value;

            if (ionization > IonizationMax)
            {
                logger.LogWarning("Ток ионизации {Current:F2} µA превышает максимум {Max} µA",
                    ionization, IonizationMax);
                await WriteFaultAsync(context, ct);
                return await WaitPhase3WithErrorAsync(context,
                    $"Ток ионизации превышен: {ionization:F2} µA", ct);
            }

            if (ionization > IonizationMin)
            {
                logger.LogInformation("Ток ионизации {Current:F2} µA в допуске", ionization);

                var continua = await context.OpcUa.WriteAsync(Continua2Tag, true, ct);
                if (continua.Error != null)
                {
                    await WriteFaultAsync(context, ct);
                    return await WaitPhase3WithErrorAsync(context,
                        $"Ошибка записи Continua_2: {continua.Error}", ct);
                }

                return await WaitPhase3Async(context, ionization, ct);
            }

            context.ReportProgress($"Ожидание розжига... {ionization:F2} µA");
            await context.DelayAsync(TimeSpan.FromMilliseconds(IonizationCheckIntervalMs), ct);
        }

        logger.LogWarning("Нет розжига котла за {Timeout} мс", NoIgnitionTimeoutMs);
        await WriteFaultAsync(context, ct);
        return await WaitPhase3WithErrorAsync(context, "Нет розжига котла (таймаут 10 сек)", ct);
    }

    /// <summary>
    /// Проверяет End/Error с коротким таймаутом. Возвращает null если ни один не сработал.
    /// </summary>
    private async Task<TestStepResult?> CheckEndOrErrorAsync(
        TestStepContext context,
        TimeSpan timeout,
        CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        try
        {
            var result = await context.TagWaiter.WaitAnyAsync(
                context.TagWaiter.CreateWaitGroup<PlcSignal>()
                    .WaitForTrue(EndTag, () => PlcSignal.End, "End")
                    .WaitForTrue(ErrorTag, () => PlcSignal.Error, "Error"),
                cts.Token);

            return result.Result switch
            {
                PlcSignal.End => await HandleSuccessAsync(context, null, ct),
                PlcSignal.Error => TestStepResult.Fail("Ошибка от PLC во время проверки ионизации"),
                _ => null
            };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return null;
        }
    }

    /// <summary>
    /// Читает ток ионизации из регистра.
    /// </summary>
    private async Task<IonizationReadResult> ReadIonizationCurrentAsync(TestStepContext context, CancellationToken ct)
    {
        var address = (ushort)(RegisterFlameIonization - _settings.BaseAddressOffset);
        var readResult = await context.DiagReader.ReadUInt16Async(address, ct);

        if (!readResult.Success)
        {
            return new IonizationReadResult(false, 0f, readResult.Error);
        }

        var ionization = readResult.Value / IonizationDivisor;
        return new IonizationReadResult(true, ionization, null);
    }

    /// <summary>
    /// Фаза 3: Ожидание End/Error для финального результата (успешный сценарий).
    /// </summary>
    private async Task<TestStepResult> WaitPhase3Async(
        TestStepContext context,
        float ionization,
        CancellationToken ct)
    {
        var result = await context.TagWaiter.WaitAnyAsync(
            context.TagWaiter.CreateWaitGroup<Phase3Result>()
                .WaitForTrue(EndTag, () => Phase3Result.End, "End")
                .WaitForTrue(ErrorTag, () => Phase3Result.Error, "Error"),
            ct);

        return result.Result switch
        {
            Phase3Result.End => await HandleSuccessAsync(context, ionization, ct),
            Phase3Result.Error => TestStepResult.Fail("Ошибка от PLC (фаза 3)"),
            _ => TestStepResult.Fail("Неизвестный результат фазы 3")
        };
    }

    /// <summary>
    /// Фаза 3 с ошибкой: ожидание End/Error после записи Fault.
    /// </summary>
    private async Task<TestStepResult> WaitPhase3WithErrorAsync(
        TestStepContext context,
        string errorMessage,
        CancellationToken ct)
    {
        var result = await context.TagWaiter.WaitAnyAsync(
            context.TagWaiter.CreateWaitGroup<Phase3Result>()
                .WaitForTrue(EndTag, () => Phase3Result.End, "End")
                .WaitForTrue(ErrorTag, () => Phase3Result.Error, "Error"),
            ct);

        return result.Result switch
        {
            Phase3Result.End => TestStepResult.Pass($"Завершено с предупреждением: {errorMessage}"),
            Phase3Result.Error => TestStepResult.Fail(errorMessage),
            _ => TestStepResult.Fail(errorMessage)
        };
    }

    /// <summary>
    /// Ожидает End или Error после ошибки (без измерения ионизации).
    /// </summary>
    private async Task<TestStepResult> WaitForEndOrErrorAsync(
        TestStepContext context,
        string errorMessage,
        CancellationToken ct)
    {
        var result = await context.TagWaiter.WaitAnyAsync(
            context.TagWaiter.CreateWaitGroup<Phase3Result>()
                .WaitForTrue(EndTag, () => Phase3Result.End, "End")
                .WaitForTrue(ErrorTag, () => Phase3Result.Error, "Error"),
            ct);

        return result.Result switch
        {
            Phase3Result.End => TestStepResult.Pass($"Завершено с предупреждением: {errorMessage}"),
            _ => TestStepResult.Fail(errorMessage)
        };
    }

    /// <summary>
    /// Обрабатывает успешное завершение шага.
    /// </summary>
    private async Task<TestStepResult> HandleSuccessAsync(
        TestStepContext context,
        float? ionization,
        CancellationToken ct)
    {
        context.Variables.Remove(HadErrorKey);
        logger.LogInformation("Шаг максимального нагрева завершён успешно");

        var writeResult = await context.OpcUa.WriteAsync(StartTag, false, ct);
        if (writeResult.Error != null)
        {
            return TestStepResult.Fail($"Ошибка сброса Start: {writeResult.Error}");
        }

        var passMsg = ionization.HasValue ? $"Ток ионизации: {ionization:F2} µA" : "";
        return TestStepResult.Pass(passMsg);
    }

    /// <summary>
    /// Записывает Fault=true в PLC.
    /// </summary>
    private async Task WriteFaultAsync(TestStepContext context, CancellationToken ct)
    {
        var writeResult = await context.OpcUa.WriteAsync(FaultTag, true, ct);
        if (writeResult.Error != null)
        {
            logger.LogWarning("Ошибка записи Fault: {Error}", writeResult.Error);
        }
    }

    private enum Phase1Result { End, Error, Ready1 }
    private enum Phase2Result { End, Error, Ready2 }
    private enum Phase3Result { End, Error }
    private enum PlcSignal { End, Error }

    /// <summary>
    /// Результат чтения тока ионизации.
    /// </summary>
    private sealed record IonizationReadResult(bool Success, float Value, string? Error);
}

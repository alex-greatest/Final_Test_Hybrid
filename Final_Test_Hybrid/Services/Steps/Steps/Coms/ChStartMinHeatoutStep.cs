using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Access;
using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Services.Steps.Steps.Coms;

/// <summary>
/// Тестовый шаг запуска минимального нагрева контура отопления.
/// На фазе Ready_2 проверяет статус котла по регистру 1005.
/// </summary>
public class ChStartMinHeatoutStep(
    AccessLevelManager accessLevelManager,
    IOptions<DiagnosticSettings> settings,
    DualLogger<ChStartMinHeatoutStep> logger) : ITestStep, IHasPlcBlockPath, IRequiresPlcSubscriptions
{
    private const string BlockPath = "DB_VI.Coms.CH_Start_Min_Heatout";
    private const string StartTag = "ns=3;s=\"DB_VI\".\"Coms\".\"CH_Start_Min_Heatout\".\"Start\"";
    private const string EndTag = "ns=3;s=\"DB_VI\".\"Coms\".\"CH_Start_Min_Heatout\".\"End\"";
    private const string ErrorTag = "ns=3;s=\"DB_VI\".\"Coms\".\"CH_Start_Min_Heatout\".\"Error\"";
    private const string Ready1Tag = "ns=3;s=\"DB_VI\".\"Coms\".\"CH_Start_Min_Heatout\".\"Ready_1\"";
    private const string Ready2Tag = "ns=3;s=\"DB_VI\".\"Coms\".\"CH_Start_Min_Heatout\".\"Ready_2\"";
    private const string Continua1Tag = "ns=3;s=\"DB_VI\".\"Coms\".\"CH_Start_Min_Heatout\".\"Сontinua_1\"";
    private const string Continua2Tag = "ns=3;s=\"DB_VI\".\"Coms\".\"CH_Start_Min_Heatout\".\"Сontinua_2\"";
    private const string FaultTag = "ns=3;s=\"DB_VI\".\"Coms\".\"CH_Start_Min_Heatout\".\"Fault\"";

    private const string HadErrorKey = "coms-ch-start-min-heatout-had-error";

    private const ushort RegisterOperationMode = 1036;
    private const ushort RegisterBoilerStatus = 1005;
    private const ushort MinHeatingMode = 3;
    private const ushort ExpectedBoilerStatus = 6;

    private readonly DiagnosticSettings _settings = settings.Value;

    public string Id => "coms-ch-start-min-heatout";
    public string Name => "Coms/CH_Start_Min_Heatout";
    public string Description => "Запуск минимального нагрева контура отопления";
    public string PlcBlockPath => BlockPath;

    public IReadOnlyList<string> RequiredPlcTags =>
        [StartTag, EndTag, ErrorTag, Ready1Tag, Ready2Tag, Continua1Tag, Continua2Tag, FaultTag];

    /// <summary>
    /// Выполняет шаг запуска минимального нагрева.
    /// </summary>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        if (context.Variables.ContainsKey(HadErrorKey))
        {
            return await HandleRetryAsync(context, ct);
        }

        return await StartMinHeatoutAsync(context, ct);
    }

    /// <summary>
    /// При retry сначала возвращает котёл в режим Стенд.
    /// </summary>
    private async Task<TestStepResult> HandleRetryAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Retry: переводим котёл в режим Стенд перед повторным запуском Min Heatout");

        var setResult = await accessLevelManager.SetStandModeAsync(context.DiagWriter, ct);
        if (!setResult.Success)
        {
            var message = $"Ошибка установки режима Стенд. {setResult.Error}";
            logger.LogError(message);
            return TestStepResult.Fail(message);
        }

        await context.DelayAsync(TimeSpan.FromMilliseconds(_settings.WriteVerifyDelayMs), ct);
        return await StartMinHeatoutAsync(context, ct);
    }

    /// <summary>
    /// Запускает шаг и ожидает первую фазу.
    /// </summary>
    private async Task<TestStepResult> StartMinHeatoutAsync(TestStepContext context, CancellationToken ct)
    {
        var startResult = await context.OpcUa.WriteAsync(StartTag, true, ct);
        if (startResult.Error != null)
        {
            return TestStepResult.Fail($"Ошибка записи Start: {startResult.Error}");
        }

        return await WaitPhase1Async(context, ct);
    }

    /// <summary>
    /// Фаза 1: ожидает End/Error/Ready_1.
    /// </summary>
    private async Task<TestStepResult> WaitPhase1Async(TestStepContext context, CancellationToken ct)
    {
        var result = await context.TagWaiter.WaitAnyAsync(
            context.TagWaiter.CreateWaitGroup<Phase1Signal>()
                .WaitForTrue(EndTag, () => Phase1Signal.End, "End")
                .WaitForTrue(ErrorTag, () => Phase1Signal.Error, "Error")
                .WaitForTrue(Ready1Tag, () => Phase1Signal.Ready1, "Ready_1"),
            ct);

        return result.Result switch
        {
            Phase1Signal.End => await HandleSuccessAsync(context, ct),
            Phase1Signal.Error => TestStepResult.Fail("Ошибка от PLC (фаза 1)"),
            Phase1Signal.Ready1 => await HandleReady1Async(context, ct),
            _ => TestStepResult.Fail("Неизвестный результат фазы 1")
        };
    }

    /// <summary>
    /// Ready_1: пишет режим min нагрева и подтверждает Continua_1.
    /// </summary>
    private async Task<TestStepResult> HandleReady1Async(TestStepContext context, CancellationToken ct)
    {
        var modeAddress = (ushort)(RegisterOperationMode - _settings.BaseAddressOffset);
        var modeWriteResult = await context.DiagWriter.WriteUInt16Async(modeAddress, MinHeatingMode, ct);

        if (!modeWriteResult.Success)
        {
            var message = $"Ошибка записи режима в регистр {RegisterOperationMode}. {modeWriteResult.Error}";
            return await FailWithFaultAsync(context, message, ct);
        }

        await context.DelayAsync(TimeSpan.FromMilliseconds(_settings.WriteVerifyDelayMs), ct);
        var modeReadResult = await context.DiagReader.ReadUInt16Async(modeAddress, ct);

        if (!modeReadResult.Success)
        {
            var message = $"Ошибка чтения регистра {RegisterOperationMode}. {modeReadResult.Error}";
            return await FailWithFaultAsync(context, message, ct);
        }

        if (modeReadResult.Value != MinHeatingMode)
        {
            var message = $"Режим не установлен (прочитано: {modeReadResult.Value}, ожидалось: {MinHeatingMode})";
            return await FailWithFaultAsync(context, message, ct);
        }

        var continuaResult = await context.OpcUa.WriteAsync(Continua1Tag, true, ct);
        if (continuaResult.Error != null)
        {
            return await FailWithFaultAsync(context, $"Ошибка записи Continua_1: {continuaResult.Error}", ct);
        }

        logger.LogInformation("Режим минимального нагрева подтверждён, ожидаем Ready_2");
        return await WaitPhase2Async(context, ct);
    }

    /// <summary>
    /// Фаза 2: ожидает End/Error/Ready_2.
    /// </summary>
    private async Task<TestStepResult> WaitPhase2Async(TestStepContext context, CancellationToken ct)
    {
        var result = await context.TagWaiter.WaitAnyAsync(
            context.TagWaiter.CreateWaitGroup<Phase2Signal>()
                .WaitForTrue(EndTag, () => Phase2Signal.End, "End")
                .WaitForTrue(ErrorTag, () => Phase2Signal.Error, "Error")
                .WaitForTrue(Ready2Tag, () => Phase2Signal.Ready2, "Ready_2"),
            ct);

        return result.Result switch
        {
            Phase2Signal.End => await HandleSuccessAsync(context, ct),
            Phase2Signal.Error => TestStepResult.Fail("Ошибка от PLC (фаза 2)"),
            Phase2Signal.Ready2 => await HandleReady2Async(context, ct),
            _ => TestStepResult.Fail("Неизвестный результат фазы 2")
        };
    }

    /// <summary>
    /// Ready_2: проверяет статус котла (1005 == 6) и подтверждает Continua_2.
    /// </summary>
    private async Task<TestStepResult> HandleReady2Async(TestStepContext context, CancellationToken ct)
    {
        var statusAddress = (ushort)(RegisterBoilerStatus - _settings.BaseAddressOffset);
        var statusResult = await context.DiagReader.ReadUInt16Async(statusAddress, ct);

        if (!statusResult.Success)
        {
            var message = $"Ошибка чтения статуса котла из регистра {RegisterBoilerStatus}. {statusResult.Error}";
            return await FailWithFaultAsync(context, message, ct);
        }

        if (statusResult.Value != ExpectedBoilerStatus)
        {
            var message = $"Статус котла некорректен: {statusResult.Value}. Ожидалось: {ExpectedBoilerStatus}";
            return await FailWithFaultAsync(context, message, ct);
        }

        var continuaResult = await context.OpcUa.WriteAsync(Continua2Tag, true, ct);
        if (continuaResult.Error != null)
        {
            return await FailWithFaultAsync(context, $"Ошибка записи Continua_2: {continuaResult.Error}", ct);
        }

        logger.LogInformation("Проверка статуса котла успешна, Continua_2 подтверждён");
        return await WaitPhase3Async(context, ct);
    }

    /// <summary>
    /// Фаза 3: ожидает End/Error после Continua_2.
    /// </summary>
    private async Task<TestStepResult> WaitPhase3Async(TestStepContext context, CancellationToken ct)
    {
        var result = await context.TagWaiter.WaitAnyAsync(
            context.TagWaiter.CreateWaitGroup<Phase3Signal>()
                .WaitForTrue(EndTag, () => Phase3Signal.End, "End")
                .WaitForTrue(ErrorTag, () => Phase3Signal.Error, "Error"),
            ct);

        return result.Result switch
        {
            Phase3Signal.End => await HandleSuccessAsync(context, ct),
            Phase3Signal.Error => TestStepResult.Fail("Ошибка от PLC (фаза 3)"),
            _ => TestStepResult.Fail("Неизвестный результат фазы 3")
        };
    }

    /// <summary>
    /// Записывает Fault=true и ждёт PLC Error.
    /// </summary>
    private async Task<TestStepResult> FailWithFaultAsync(
        TestStepContext context,
        string errorMessage,
        CancellationToken ct)
    {
        context.Variables[HadErrorKey] = true;
        logger.LogError(errorMessage);
        await WriteFaultAsync(context, ct);
        return await WaitForPlcErrorAfterFaultAsync(context, errorMessage, ct);
    }

    /// <summary>
    /// После Fault ожидаем только Error от PLC.
    /// </summary>
    private async Task<TestStepResult> WaitForPlcErrorAfterFaultAsync(
        TestStepContext context,
        string errorMessage,
        CancellationToken ct)
    {
        var result = await context.TagWaiter.WaitAnyAsync(
            context.TagWaiter.CreateWaitGroup<FaultSignal>()
                .WaitForTrue(ErrorTag, () => FaultSignal.Error, "Error"),
            ct);

        return result.Result switch
        {
            FaultSignal.Error => TestStepResult.Fail(errorMessage),
            _ => TestStepResult.Fail(errorMessage)
        };
    }

    /// <summary>
    /// Обрабатывает успешное завершение шага.
    /// </summary>
    private async Task<TestStepResult> HandleSuccessAsync(TestStepContext context, CancellationToken ct)
    {
        context.Variables.Remove(HadErrorKey);

        var resetStartResult = await context.OpcUa.WriteAsync(StartTag, false, ct);
        if (resetStartResult.Error != null)
        {
            return TestStepResult.Fail($"Ошибка сброса Start: {resetStartResult.Error}");
        }

        logger.LogInformation("Шаг минимального нагрева завершён успешно");
        return TestStepResult.Pass();
    }

    /// <summary>
    /// Пишет Fault=true в PLC.
    /// </summary>
    private async Task WriteFaultAsync(TestStepContext context, CancellationToken ct)
    {
        var faultResult = await context.OpcUa.WriteAsync(FaultTag, true, ct);
        if (faultResult.Error != null)
        {
            logger.LogWarning("Ошибка записи Fault: {Error}", faultResult.Error);
        }
    }

    private enum Phase1Signal { End, Error, Ready1 }
    private enum Phase2Signal { End, Error, Ready2 }
    private enum Phase3Signal { End, Error }
    private enum FaultSignal { Error }
}

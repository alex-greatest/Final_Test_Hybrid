using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Final_Test_Hybrid.Services.OpcUa.WaitGroup;
using Final_Test_Hybrid.Services.Results;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Limits;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Services.Steps.Steps.CH;

/// <summary>
/// Тестовый шаг сравнения температуры холодной воды из датчика стенда (OPC-UA)
/// с температурой из датчика котла (Modbus). Вычисляет дельту и проверяет допуски.
/// </summary>
public class CompareFlowNtcTemperatureColdStep(
    IOptions<DiagnosticSettings> settings,
    DualLogger<CompareFlowNtcTemperatureColdStep> logger,
    ITestResultsService testResultsService) : ITestStep, IHasPlcBlockPath, IRequiresPlcSubscriptions, IRequiresRecipes, IProvideLimits
{
    private static readonly TimeSpan ErrorConfirmationWindow = TimeSpan.FromMilliseconds(150);
    private const string BlockPath = "DB_VI.CH.Compare_Flow_NTC_Temperature_Cold";
    private const string StartTag = "ns=3;s=\"DB_VI\".\"CH\".\"Compare_Flow_NTC_Temperature_Cold\".\"Start\"";
    private const string EndTag = "ns=3;s=\"DB_VI\".\"CH\".\"Compare_Flow_NTC_Temperature_Cold\".\"End\"";
    private const string ErrorTag = "ns=3;s=\"DB_VI\".\"CH\".\"Compare_Flow_NTC_Temperature_Cold\".\"Error\"";
    private const string Ready1Tag = "ns=3;s=\"DB_VI\".\"CH\".\"Compare_Flow_NTC_Temperature_Cold\".\"Ready_1\"";
    private const string Continua1Tag = "ns=3;s=\"DB_VI\".\"CH\".\"Compare_Flow_NTC_Temperature_Cold\".\"Сontinua_1\"";
    private const string FaultTag = "ns=3;s=\"DB_VI\".\"CH\".\"Compare_Flow_NTC_Temperature_Cold\".\"Fault\"";
    private const string ChTmrTag = "ns=3;s=\"DB_Measure\".\"Temper\".\"CH_TMR\"";
    private const string TFlowDeltaMaxRecipe = "ns=3;s=\"DB_Recipe\".\"CH\".\"TFlowDeltaMax\"";
    private const string ParameterName = "Delta_CH_Flow_Temp_Cold";
    private const ushort RegisterChTemperature = 1006;

    private readonly DiagnosticSettings _settings = settings.Value;

    public string Id => "ch-compare-flow-ntc-temperature-cold";
    public string Name => "CH/Compare_Flow_NTC_Temperature_Cold";
    public string Description => "Контур Отопление. Сравнение показаний температур холодной воды";
    public string PlcBlockPath => BlockPath;
    public IReadOnlyList<string> RequiredPlcTags => [StartTag, EndTag, ErrorTag, Ready1Tag, Continua1Tag, FaultTag, ChTmrTag];
    public IReadOnlyList<string> RequiredRecipeAddresses => [TFlowDeltaMaxRecipe];

    public string? GetLimits(LimitsContext context)
    {
        var maxDelta = context.RecipeProvider.GetValue<float>(TFlowDeltaMaxRecipe);
        return maxDelta != null ? $"<= {maxDelta:F1}" : "<= 3.0";
    }

    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Запуск сравнения температуры NTC (холодная вода)");
        testResultsService.Remove(ParameterName);

        var writeResult = await context.OpcUa.WriteAsync(StartTag, true, ct);
        if (writeResult.Error != null)
        {
            return TestStepResult.Fail($"Ошибка записи Start: {writeResult.Error}");
        }

        return await WaitPhase1Async(context, ct);
    }

    private async Task<TestStepResult> WaitPhase1Async(TestStepContext context, CancellationToken ct)
    {
        var result = await WaitForPhase1SignalAsync(context, ct);
        return result switch
        {
            Phase1Result.End => await HandleCompletionAsync(context, null, isSuccess: true, ct),
            Phase1Result.Error => await HandleCompletionAsync(context, null, isSuccess: false, ct),
            Phase1Result.Ready1 => await HandleReady1Async(context, ct),
            _ => TestStepResult.Fail("Неизвестный результат")
        };
    }

    private async Task<TestStepResult> HandleReady1Async(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Ready_1 получен, начинаю чтение температур");

        var (_, chTmr, opcError) = await context.OpcUa.ReadAsync<float>(ChTmrTag, ct);
        if (opcError != null)
        {
            var msg = $"Ошибка чтения CH_TMR из OPC-UA: {opcError}";
            logger.LogError(msg);
            await WriteFaultAsync(context, ct);
            return TestStepResult.Fail(msg);
        }

        var address = (ushort)(RegisterChTemperature - _settings.BaseAddressOffset);
        var modbusResult = await context.PacedDiagReader.ReadInt16Async(address, ct);
        if (!modbusResult.Success)
        {
            var msg = $"Ошибка при чтении температуры котла из регистра {RegisterChTemperature}. {modbusResult.Error}";
            logger.LogError(msg);
            await WriteFaultAsync(context, ct);
            return TestStepResult.Fail(msg);
        }

        var modbusTemp = modbusResult.Value;
        var delta = Math.Abs(chTmr - modbusTemp);

        var maxDelta = context.RecipeProvider.GetValue<float>(TFlowDeltaMaxRecipe) ?? 3.0f;
        var measurement = new DeltaMeasurement(delta, chTmr, modbusTemp, maxDelta);
        logger.LogInformation(
            "Дельта: {Delta:F3}, TMR (из ПЛК): {ChTmr:F3}, котёл (регистр): {ModbusTemp}",
            delta, chTmr, modbusTemp);

        if (delta <= maxDelta)
        {
            var writeResult = await context.OpcUa.WriteAsync(Continua1Tag, true, ct);
            if (writeResult.Error != null)
            {
                logger.LogError("Ошибка записи Continua_1: {Error}, пишем Fault", writeResult.Error);
                await WriteFaultAsync(context, ct);
            }
        }
        else
        {
            logger.LogWarning("Дельта {Delta:F3} превышает допуск {MaxDelta:F3}", delta, maxDelta);
            await WriteFaultAsync(context, ct);
        }

        return await WaitPhase2Async(context, measurement, ct);
    }

    private async Task<TestStepResult> WaitPhase2Async(TestStepContext context, DeltaMeasurement? measurement, CancellationToken ct)
    {
        var result = await WaitForPhase2SignalAsync(context, ct);
        return result switch
        {
            Phase2Result.End => await HandleCompletionAsync(context, measurement, isSuccess: true, ct),
            Phase2Result.Error => await HandleCompletionAsync(context, measurement, isSuccess: false, ct),
            _ => TestStepResult.Fail("Неизвестный результат")
        };
    }

    private async Task<TestStepResult> HandleCompletionAsync(
        TestStepContext context,
        DeltaMeasurement? measurement,
        bool isSuccess,
        CancellationToken ct)
    {
        if (measurement != null)
        {
            var status = isSuccess ? 1 : 2;

            testResultsService.Add(
                parameterName: ParameterName,
                value: $"{measurement.Delta:F3}",
                min: "0.000",
                max: $"{measurement.MaxDelta:F3}",
                status: status,
                isRanged: true,
                unit: "",
                test: Name);

            logger.LogInformation(
                "Результат: Дельта={Delta:F3}, TMR={ChTmr:F3}, котёл={ModbusTemp}, статус={Status}",
                measurement.Delta, measurement.ChTmr, measurement.ModbusTemp,
                isSuccess ? "OK" : "NOK");
        }

        var msg = measurement != null
            ? $"Дельта: {measurement.Delta:F3}, TMR: {measurement.ChTmr:F3}, котёл: {measurement.ModbusTemp}"
            : "Ошибка сравнения температуры NTC";

        if (isSuccess)
        {
            logger.LogInformation("Сравнение температуры NTC завершено успешно");

            var writeResult = await context.OpcUa.WriteAsync(StartTag, false, ct);
            return writeResult.Error != null ? TestStepResult.Fail($"Ошибка сброса Start: {writeResult.Error}") : TestStepResult.Pass(msg);
        }

        return TestStepResult.Fail(msg);
    }

    private async Task WriteFaultAsync(TestStepContext context, CancellationToken ct)
    {
        var writeResult = await context.OpcUa.WriteAsync(FaultTag, true, ct);
        if (writeResult.Error != null)
        {
            logger.LogWarning("Ошибка записи Fault: {Error}", writeResult.Error);
        }
    }

    private async Task<Phase1Result> WaitForPhase1SignalAsync(
        TestStepContext context,
        CancellationToken ct)
    {
        while (true)
        {
            var result = await context.TagWaiter.WaitAnyAsync(CreatePhase1WaitGroup(context), ct);
            var resolved = await ResolvePhase1SignalAsync(context, result.Result, ct);
            if (resolved != null)
            {
                return resolved.Value;
            }
        }
    }

    private async Task<Phase2Result> WaitForPhase2SignalAsync(
        TestStepContext context,
        CancellationToken ct)
    {
        while (true)
        {
            var result = await context.TagWaiter.WaitAnyAsync(CreatePhase2WaitGroup(context), ct);
            var resolved = await ResolvePhase2SignalAsync(context, result.Result, ct);
            if (resolved != null)
            {
                return resolved.Value;
            }
        }
    }

    private WaitGroupBuilder<Phase1Result> CreatePhase1WaitGroup(TestStepContext context)
    {
        return context.TagWaiter.CreateWaitGroup<Phase1Result>()
            .WaitForTrue(EndTag, () => Phase1Result.End, "End")
            .WaitForTrue(ErrorTag, () => Phase1Result.Error, "Error")
            .WaitForTrue(Ready1Tag, () => Phase1Result.Ready1, "Ready_1");
    }

    private WaitGroupBuilder<Phase2Result> CreatePhase2WaitGroup(TestStepContext context)
    {
        return context.TagWaiter.CreateWaitGroup<Phase2Result>()
            .WaitForTrue(EndTag, () => Phase2Result.End, "End")
            .WaitForTrue(ErrorTag, () => Phase2Result.Error, "Error");
    }

    private async Task<Phase1Result?> ResolvePhase1SignalAsync(
        TestStepContext context,
        Phase1Result result,
        CancellationToken ct)
    {
        if (result == Phase1Result.End)
        {
            return Phase1Result.End;
        }

        if (result == Phase1Result.Ready1)
        {
            var terminalSignal = await TryGetTerminalSignalAsync(context, ct);
            return terminalSignal switch
            {
                null => Phase1Result.Ready1,
                TerminalSignal.End => Phase1Result.End,
                _ => await ConfirmErrorSignalAsync(context, ct) switch
                {
                    TerminalSignal.End => Phase1Result.End,
                    TerminalSignal.Error => Phase1Result.Error,
                    _ => null
                }
            };
        }

        return await ConfirmErrorSignalAsync(context, ct) switch
        {
            TerminalSignal.End => Phase1Result.End,
            TerminalSignal.Error => Phase1Result.Error,
            _ => null
        };
    }

    private async Task<Phase2Result?> ResolvePhase2SignalAsync(
        TestStepContext context,
        Phase2Result result,
        CancellationToken ct)
    {
        if (result == Phase2Result.End)
        {
            return Phase2Result.End;
        }

        return await ConfirmErrorSignalAsync(context, ct) switch
        {
            TerminalSignal.End => Phase2Result.End,
            TerminalSignal.Error => Phase2Result.Error,
            _ => null
        };
    }

    private async Task<TerminalSignal?> TryGetTerminalSignalAsync(
        TestStepContext context,
        CancellationToken ct)
    {
        try
        {
            var result = await context.TagWaiter.WaitAnyAsync(
                context.TagWaiter.CreateWaitGroup<TerminalSignal>()
                    .WaitForTrue(EndTag, () => TerminalSignal.End, "End")
                    .WaitForTrue(ErrorTag, () => TerminalSignal.Error, "Error")
                    .WithTimeout(TimeSpan.Zero),
                ct);

            return result.Result;
        }
        catch (TimeoutException)
        {
            return null;
        }
    }

    private async Task<TerminalSignal?> ConfirmErrorSignalAsync(
        TestStepContext context,
        CancellationToken ct)
    {
        try
        {
            var result = await context.TagWaiter.WaitAnyAsync(
                context.TagWaiter.CreateWaitGroup<ErrorConfirmationResult>()
                    .WaitForTrue(EndTag, () => ErrorConfirmationResult.End, "End")
                    .WaitForFalse(ErrorTag, () => ErrorConfirmationResult.Cleared, "ErrorCleared")
                    .WithTimeout(ErrorConfirmationWindow),
                ct);

            if (result.Result == ErrorConfirmationResult.End)
            {
                logger.LogInformation("End получен в окне подтверждения Error");
                return TerminalSignal.End;
            }

            logger.LogInformation("Error сброшен в окне подтверждения, продолжаю ожидание");
            return null;
        }
        catch (TimeoutException)
        {
            var terminalSignal = await TryGetTerminalSignalAsync(context, ct);
            if (terminalSignal == TerminalSignal.End)
            {
                logger.LogInformation("End получил приоритет над Error на границе окна подтверждения");
                return TerminalSignal.End;
            }

            logger.LogWarning("Error удерживается {Duration} мс, фиксирую ошибку", ErrorConfirmationWindow.TotalMilliseconds);
            return TerminalSignal.Error;
        }
    }

    private enum Phase1Result { End, Error, Ready1 }
    private enum Phase2Result { End, Error }
    private enum TerminalSignal { End, Error }
    private enum ErrorConfirmationResult { End, Cleared }

    private sealed record DeltaMeasurement(float Delta, float ChTmr, short ModbusTemp, float MaxDelta);
}

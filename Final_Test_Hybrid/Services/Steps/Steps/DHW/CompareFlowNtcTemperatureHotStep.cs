using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Final_Test_Hybrid.Services.Results;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Limits;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Services.Steps.Steps.DHW;

/// <summary>
/// Тестовый шаг сравнения температуры горячей воды из датчика стенда (OPC-UA)
/// с температурой из датчика котла (Modbus). Вычисляет дельту и проверяет допуски.
/// </summary>
public class CompareFlowNtcTemperatureHotStep(
    IOptions<DiagnosticSettings> settings,
    DualLogger<CompareFlowNtcTemperatureHotStep> logger,
    ITestResultsService testResultsService) : ITestStep, IHasPlcBlockPath, IRequiresPlcSubscriptions, IRequiresRecipes, IProvideLimits
{
    private const string BlockPath = "DB_VI.DHW.Compare_Flow_NTC_Temperature_Hot";
    private const string StartTag = "ns=3;s=\"DB_VI\".\"DHW\".\"Compare_Flow_NTC_Temperature_Hot\".\"Start\"";
    private const string EndTag = "ns=3;s=\"DB_VI\".\"DHW\".\"Compare_Flow_NTC_Temperature_Hot\".\"End\"";
    private const string ErrorTag = "ns=3;s=\"DB_VI\".\"DHW\".\"Compare_Flow_NTC_Temperature_Hot\".\"Error\"";
    private const string Ready1Tag = "ns=3;s=\"DB_VI\".\"DHW\".\"Compare_Flow_NTC_Temperature_Hot\".\"Ready_1\"";
    private const string Continua1Tag = "ns=3;s=\"DB_VI\".\"DHW\".\"Compare_Flow_NTC_Temperature_Hot\".\"Сontinua_1\"";
    private const string FaultTag = "ns=3;s=\"DB_VI\".\"DHW\".\"Compare_Flow_NTC_Temperature_Hot\".\"Fault\"";
    private const string DhwTusTag = "ns=3;s=\"DB_Measure\".\"Temper\".\"DHW_TUS\"";
    private const string TFlowDeltaMaxRecipe = "ns=3;s=\"DB_Recipe\".\"DHW\".\"TFlowDeltaMax\"";
    private const string ParameterName = "Delta_DHW_Flow_Temp_Hot";
    private const ushort RegisterDhwTemperature = 1009;

    private readonly DiagnosticSettings _settings = settings.Value;

    public string Id => "dhw-compare-flow-ntc-temperature-hot";
    public string Name => "DHW/Compare_Flow_NTC_Temperature_Hot";
    public string Description => "Контур ГВС. Сравнение показаний температур горячей воды";
    public string PlcBlockPath => BlockPath;
    public IReadOnlyList<string> RequiredPlcTags => [StartTag, EndTag, ErrorTag, Ready1Tag, Continua1Tag, FaultTag, DhwTusTag];
    public IReadOnlyList<string> RequiredRecipeAddresses => [TFlowDeltaMaxRecipe];

    /// <summary>
    /// Возвращает пределы для отображения в гриде.
    /// </summary>
    /// <param name="context">Контекст с индексом колонки и провайдером рецептов.</param>
    /// <returns>Строка с пределами или null, если пределы недоступны.</returns>
    public string? GetLimits(LimitsContext context)
    {
        var maxDelta = context.RecipeProvider.GetValue<float>(TFlowDeltaMaxRecipe);
        return maxDelta != null ? $"<= {maxDelta:F1}" : "<= 3.0";
    }

    /// <summary>
    /// Выполняет шаг сравнения температуры NTC (горячая вода).
    /// </summary>
    /// <param name="context">Контекст выполнения шага.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Запуск сравнения температуры NTC (горячая вода)");
        testResultsService.Remove(ParameterName);

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
            Phase1Result.End => await HandleCompletionAsync(context, null, isSuccess: true, ct),
            Phase1Result.Error => await HandleCompletionAsync(context, null, isSuccess: false, ct),
            Phase1Result.Ready1 => await HandleReady1Async(context, ct),
            _ => TestStepResult.Fail("Неизвестный результат")
        };
    }

    /// <summary>
    /// Обрабатывает Ready_1: читает температуры, вычисляет дельту, записывает Continua_1 или Fault.
    /// </summary>
    private async Task<TestStepResult> HandleReady1Async(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Ready_1 получен, начинаю чтение температур");

        // Читаем температуру из стенда (OPC-UA)
        var (_, dhwTus, opcError) = await context.OpcUa.ReadAsync<float>(DhwTusTag, ct);
        if (opcError != null)
        {
            var msg = $"Ошибка чтения DHW_TUS из OPC-UA: {opcError}";
            logger.LogError(msg);
            await WriteFaultAsync(context, ct);
            return TestStepResult.Fail(msg);
        }

        // Читаем температуру из котла (Modbus)
        var address = (ushort)(RegisterDhwTemperature - _settings.BaseAddressOffset);
        var modbusResult = await context.DiagReader.ReadInt16Async(address, ct);
        if (!modbusResult.Success)
        {
            var msg = $"Ошибка при чтении температуры котла из регистра {RegisterDhwTemperature}. {modbusResult.Error}";
            logger.LogError(msg);
            await WriteFaultAsync(context, ct);
            return TestStepResult.Fail(msg);
        }

        var modbusTemp = modbusResult.Value;
        var delta = Math.Abs(dhwTus - modbusTemp);

        // Получаем максимально допустимую дельту из рецепта
        var maxDelta = context.RecipeProvider.GetValue<float>(TFlowDeltaMaxRecipe) ?? 3.0f;

        var measurement = new DeltaMeasurement(delta, dhwTus, modbusTemp, maxDelta);

        logger.LogInformation(
            "Дельта: {Delta:F3}, TUS (из ПЛК): {DhwTus:F3}, котёл (регистр): {ModbusTemp}",
            delta, dhwTus, modbusTemp);

        // Записываем Continua_1 или Fault в зависимости от дельты
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

    /// <summary>
    /// Фаза 2: Ожидание End/Error после Ready_1.
    /// </summary>
    private async Task<TestStepResult> WaitPhase2Async(TestStepContext context, DeltaMeasurement? measurement, CancellationToken ct)
    {
        var result = await context.TagWaiter.WaitAnyAsync(
            context.TagWaiter.CreateWaitGroup<Phase2Result>()
                .WaitForTrue(EndTag, () => Phase2Result.End, "End")
                .WaitForTrue(ErrorTag, () => Phase2Result.Error, "Error"),
            ct);

        return result.Result switch
        {
            Phase2Result.End => await HandleCompletionAsync(context, measurement, isSuccess: true, ct),
            Phase2Result.Error => await HandleCompletionAsync(context, measurement, isSuccess: false, ct),
            _ => TestStepResult.Fail("Неизвестный результат")
        };
    }

    /// <summary>
    /// Обрабатывает завершение шага: сохраняет результат, возвращает Pass/Fail.
    /// </summary>
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
                "Результат: Дельта={Delta:F3}, TUS={DhwTus:F3}, котёл={ModbusTemp}, статус={Status}",
                measurement.Delta, measurement.DhwTus, measurement.ModbusTemp,
                isSuccess ? "OK" : "NOK");
        }

        var msg = measurement != null
            ? $"Дельта: {measurement.Delta:F3}, TUS: {measurement.DhwTus:F3}, котёл: {measurement.ModbusTemp}"
            : "Ошибка сравнения температуры NTC";

        if (isSuccess)
        {
            logger.LogInformation("Сравнение температуры NTC завершено успешно");

            var writeResult = await context.OpcUa.WriteAsync(StartTag, false, ct);
            return writeResult.Error != null ? TestStepResult.Fail($"Ошибка сброса Start: {writeResult.Error}") : TestStepResult.Pass(msg);
        }

        return TestStepResult.Fail(msg);
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
    private enum Phase2Result { End, Error }

    /// <summary>
    /// Запись измерения дельты температур.
    /// </summary>
    private sealed record DeltaMeasurement(float Delta, float DhwTus, short ModbusTemp, float MaxDelta);
}

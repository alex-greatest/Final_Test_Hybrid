using Final_Test_Hybrid.Models;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Final_Test_Hybrid.Services.Results;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Services.Steps.Steps.Coms;

/// <summary>
/// Тестовый шаг чтения и верификации параметров SoftCodePlug из ЭБУ котла.
/// </summary>
public partial class ReadSoftCodePlugStep(
    BoilerState boilerState,
    IOptions<DiagnosticSettings> settings,
    ITestResultsService testResultsService,
    DualLogger<ReadSoftCodePlugStep> logger) : ITestStep, IRequiresRecipes
{
    // Регистры для чтения (адреса из документации)
    private const ushort RegisterNomenclatureNumber = 1139;
    private const ushort RegisterBoilerPowerType = 1002;
    private const ushort RegisterPumpType = 1003;
    private const ushort RegisterPressureDeviceType = 1004;
    private const ushort RegisterGasRegulatorType = 1157;
    private const ushort RegisterMaxHeatOutputCh = 1050;
    private const ushort RegisterMaxHeatOutputDhw = 1051;
    private const ushort RegisterMinHeatOutputCh = 1053;
    private const ushort RegisterPumpMode = 1108;
    private const ushort RegisterPumpPower = 1109;
    private const ushort RegisterGasType = 1065;
    private const ushort RegisterCurrentOffset = 1030;
    private const ushort RegisterFlowCoefficientHi = 1171;
    private const ushort RegisterPumpPowerMax = 1161;
    private const ushort RegisterPumpPowerMin = 1160;
    private const ushort RegisterComfortHysteresis = 1031;
    private const ushort RegisterMaxFlowTemperature = 1052;
    private const ushort RegisterItelmaArticle = 1182;
    private const ushort RegisterProductionDate = 1133;
    private const ushort RegisterSupplierCodeHi = 1131;
    private const ushort RegisterCounterNumberHi = 1137;
    private const ushort RegisterThermostatJumper = 1071;
    private const int NomenclatureMaxLength = 14;
    private const int ProductionDateMaxLength = 8;

    // Рецепты для верификации
    private const string BoilerTypeRecipe = "Boiler_Type";
    private const string BoilerTypeMinRecipe = "Boiler_Type_Min";
    private const string BoilerTypeMaxRecipe = "Boiler_Type_Max";
    private const string PumpTypeRecipe = "Pump_Type";
    private const string PumpTypeMinRecipe = "Pump_Type_Min";
    private const string PumpTypeMaxRecipe = "Pump_Type_Max";
    private const string PresSenTypeRecipe = "Pres_Sen_Type";
    private const string PresSensorMinRecipe = "Pres_Sensor_Min";
    private const string PresSensorMaxRecipe = "Pres_Sensor_Max";
    private const string GasValveTypeRecipe = "Gas_Valve_Type";
    private const string GasValveTypeMinRecipe = "Gas_Valve_Type_Min";
    private const string GasValveTypeMaxRecipe = "Gas_Valve_Type_Max";
    private const string MaxChHeatOutRecipe = "Max_CH_HeatOut";
    private const string MaxChHeatOutMinRecipe = "Max_CH_HeatOut_Min";
    private const string MaxChHeatOutMaxRecipe = "Max_CH_HeatOut_Max";
    private const string MaxDhwHeatOutRecipe = "Max_DHW_HeatOut";
    private const string MaxDhwHeatOutMinRecipe = "Max_DHW_HeatOut_Min";
    private const string MaxDhwHeatOutMaxRecipe = "Max_DHW_HeatOut_Max";
    private const string MinChHeatOutRecipe = "Min_CH_HeatOut";
    private const string MinChHeatOutMinRecipe = "Min_CH_HeatOut_Min";
    private const string MinChHeatOutMaxRecipe = "Min_CH_HeatOut_Max";
    private const string PumpModeRecipe = "Pump_Mode";
    private const string PumpModeMinRecipe = "Pump_Mode_Min";
    private const string PumpModeMaxRecipe = "Pump_Mode_Max";
    private const string PumpPowerRecipe = "Pump_Power";
    private const string PumpPowerMinRecipe = "Pump_Power_Min";
    private const string PumpPowerMaxRecipe = "Pump_Power_Max";
    private const string GasTypeRecipe = "Gas_Type";
    private const string GasTypeMinRecipe = "Gas_Type_Min";
    private const string GasTypeMaxRecipe = "Gas_Type_Max";
    private const string CurrentOffsetRecipe = "Current_Offset";
    private const string CurrentOffsetMinRecipe = "Current_Offset_Min";
    private const string CurrentOffsetMaxRecipe = "Current_Offset_Max";
    private const string FlowCoefficientRecipe = "Flow_Coefficient";
    private const string FlowCoefficientMinRecipe = "Flow_Coefficient_Min";
    private const string FlowCoefficientMaxRecipe = "Flow_Coefficient_Max";
    private const string MaxPumpAutoPowerRecipe = "Max_Pump_AutoModePower_Max";
    private const string MaxPumpAutoPowerMinRecipe = "Max_Pump_AutoModePower_Min";
    private const string MaxPumpAutoPowerMaxRecipe = "Max_Pump_AutoModePower_Max";
    private const string MinPumpAutoPowerRecipe = "Min_Pump_AutoModePower_Min";
    private const string MinPumpAutoPowerMinRecipe = "Min_Pump_AutoModePower_Min";
    private const string MinPumpAutoPowerMaxRecipe = "Min_Pump_AutoModePower_Max";
    private const string ComfortHysteresisRecipe = "Comfort_Hysteresis";
    private const string ComfortHysteresisMinRecipe = "Comfort_Hysteresis_Min";
    private const string ComfortHysteresisMaxRecipe = "Comfort_Hysteresis_Max";
    private const string MaxFlowTemperatureRecipe = "Max_Flow_Temperature";
    private const string MaxFlowTemperatureMinRecipe = "Max_Flow_Temperature_Min";
    private const string MaxFlowTemperatureMaxRecipe = "Max_Flow_Temperature_Max";
    private const string NumberOfContoursRecipe = "NumberOfContours";

    // Имена параметров для результатов
    private const string ArticleResultName = "Nomenclature_EngP3";
    private const string BoilerTypeResultName = "Boiler_Type";
    private const string PumpTypeResultName = "Type_pumps";
    private const string PresSensorResultName = "Pres_sensor";
    private const string GasValveTypeResultName = "Gas_Valve_Type";
    private const string MaxChHeatOutResultName = "Max_CH_HeatOut";
    private const string MaxDhwHeatOutResultName = "Max_DHW_HeatOut";
    private const string MinChHeatOutResultName = "Min_CH_HeatOut";
    private const string PumpModeResultName = "Pump_Mode";
    private const string PumpPowerResultName = "Pump_Power";
    private const string GasTypeResultName = "Gas_Type";
    private const string CurrentOffsetResultName = "Current_Offset";
    private const string FlowCoefficientResultName = "Flow_Coefficient";
    private const string MaxPumpAutoPowerResultName = "Max_Pump_AutoModePower";
    private const string MinPumpAutoPowerResultName = "Min_Pump_AutoModePower";
    private const string ComfortHysteresisResultName = "Comfort_Hysteresis";
    private const string MaxFlowTemperatureResultName = "Max_Flow_Temperature";
    private const string ItelmaArticleResultName = "Nomenclature_ITELMA";
    private const string ProductionDateResultName = "Month_Date";
    private const string SupplierCodeResultName = "Supplier_Code";
    private const string CounterNumberResultName = "Counter_Number";

    private readonly DiagnosticSettings _settings = settings.Value;

    public string Id => "coms-read-soft-code-plug";
    public string Name => "Coms/Read_Soft_Code_Plug";
    public string Description => "Чтение параметров из ЭБУ";

    public IReadOnlyList<string> RequiredRecipeAddresses =>
    [
        BoilerTypeRecipe, BoilerTypeMinRecipe, BoilerTypeMaxRecipe,
        PumpTypeRecipe, PumpTypeMinRecipe, PumpTypeMaxRecipe,
        PresSenTypeRecipe, PresSensorMinRecipe, PresSensorMaxRecipe,
        GasValveTypeRecipe, GasValveTypeMinRecipe, GasValveTypeMaxRecipe,
        MaxChHeatOutRecipe, MaxChHeatOutMinRecipe, MaxChHeatOutMaxRecipe,
        MaxDhwHeatOutRecipe, MaxDhwHeatOutMinRecipe, MaxDhwHeatOutMaxRecipe,
        MinChHeatOutRecipe, MinChHeatOutMinRecipe, MinChHeatOutMaxRecipe,
        PumpModeRecipe, PumpModeMinRecipe, PumpModeMaxRecipe,
        PumpPowerRecipe, PumpPowerMinRecipe, PumpPowerMaxRecipe,
        GasTypeRecipe, GasTypeMinRecipe, GasTypeMaxRecipe,
        CurrentOffsetRecipe, CurrentOffsetMinRecipe, CurrentOffsetMaxRecipe,
        FlowCoefficientRecipe, FlowCoefficientMinRecipe, FlowCoefficientMaxRecipe,
        MaxPumpAutoPowerRecipe, MaxPumpAutoPowerMinRecipe, MaxPumpAutoPowerMaxRecipe,
        MinPumpAutoPowerRecipe, MinPumpAutoPowerMinRecipe, MinPumpAutoPowerMaxRecipe,
        ComfortHysteresisRecipe, ComfortHysteresisMinRecipe, ComfortHysteresisMaxRecipe,
        MaxFlowTemperatureRecipe, MaxFlowTemperatureMinRecipe, MaxFlowTemperatureMaxRecipe,
        NumberOfContoursRecipe
    ];

    /// <summary>
    /// Выполняет чтение и верификацию всех параметров SoftCodePlug из ЭБУ котла.
    /// </summary>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        ClearPreviousResults();

        // Задержка перед чтением для гарантии обновления данных в ЭБУ
        logger.LogDebug("Ожидание {Delay} мс перед чтением параметров", _settings.WriteVerifyDelayMs);
        await context.DelayAsync(TimeSpan.FromMilliseconds(_settings.WriteVerifyDelayMs), ct);

        // Подшаг 1: Чтение и проверка артикула
        var result = await ReadAndVerifyArticleAsync(context, ct);
        if (!result.Success) return result;

        // Подшаг 2: Чтение и проверка типа котла
        result = await ReadAndVerifyBoilerTypeAsync(context, ct);
        if (!result.Success) return result;

        // Подшаг 3: Чтение и проверка типа насоса
        result = await ReadAndVerifyPumpTypeAsync(context, ct);
        if (!result.Success) return result;

        // Подшаг 4: Чтение и проверка типа датчика давления
        result = await ReadAndVerifyPressureDeviceTypeAsync(context, ct);
        if (!result.Success) return result;

        // Подшаг 5: Чтение и проверка типа регулятора газа
        result = await ReadAndVerifyGasRegulatorTypeAsync(context, ct);
        if (!result.Success) return result;

        // Подшаг 6: Чтение и проверка макс. теплопроизводительности отопления
        result = await ReadAndVerifyMaxChHeatOutputAsync(context, ct);
        if (!result.Success) return result;

        // Подшаг 7: Чтение и проверка макс. теплопроизводительности ГВС
        result = await ReadAndVerifyMaxDhwHeatOutputAsync(context, ct);
        if (!result.Success) return result;

        // Подшаг 8: Чтение и проверка мин. теплопроизводительности отопления
        result = await ReadAndVerifyMinChHeatOutputAsync(context, ct);
        if (!result.Success) return result;

        // Подшаг 9: Чтение и проверка режима работы насоса
        result = await ReadAndVerifyPumpModeAsync(context, ct);
        if (!result.Success) return result;

        // Подшаг 10: Чтение и проверка установленной мощности насоса
        result = await ReadAndVerifyPumpPowerAsync(context, ct);
        if (!result.Success) return result;

        // Подшаг 11: Чтение и проверка вида газа
        result = await ReadAndVerifyGasTypeAsync(context, ct);
        if (!result.Success) return result;

        // Подшаг 12: Чтение и проверка сдвига тока на модуляционной катушке
        result = await ReadAndVerifyCurrentOffsetAsync(context, ct);
        if (!result.Success) return result;

        // Подшаг 13: Чтение и проверка коэффициента k расхода воды (только для двухконтурных)
        if (IsDualCircuit(context))
        {
            result = await ReadAndVerifyFlowCoefficientAsync(context, ct);
            if (!result.Success) return result;
        }
        else
        {
            logger.LogInformation("Пропуск чтения коэффициента k — одноконтурный котёл");
        }

        // Подшаг 14: Чтение и проверка макс. мощности насоса в авто режиме
        result = await ReadAndVerifyMaxPumpAutoPowerAsync(context, ct);
        if (!result.Success) return result;

        // Подшаг 15: Чтение и проверка мин. мощности насоса в авто режиме
        result = await ReadAndVerifyMinPumpAutoPowerAsync(context, ct);
        if (!result.Success) return result;

        // Подшаг 16: Чтение и проверка гистерезиса ГВС в режиме комфорт (только для двухконтурных)
        if (IsDualCircuit(context))
        {
            result = await ReadAndVerifyComfortHysteresisAsync(context, ct);
            if (!result.Success) return result;
        }
        else
        {
            logger.LogInformation("Пропуск чтения гистерезиса ГВС — одноконтурный котёл");
        }

        // Подшаг 17: Чтение и проверка макс. температуры подающей линии
        result = await ReadAndVerifyMaxFlowTemperatureAsync(context, ct);
        if (!result.Success) return result;

        // Подшаг 18: Чтение артикула ИТЭЛМА (без верификации)
        result = await ReadItelmaArticleAsync(context, ct);
        if (!result.Success) return result;

        // Подшаг 19: Чтение даты производства (без верификации)
        result = await ReadProductionDateAsync(context, ct);
        if (!result.Success) return result;

        // Подшаг 20: Чтение кода поставщика (без верификации)
        result = await ReadSupplierCodeAsync(context, ct);
        if (!result.Success) return result;

        // Подшаг 21: Чтение порядкового номера (без верификации)
        result = await ReadCounterNumberAsync(context, ct);
        if (!result.Success) return result;

        // Подшаг 22: Проверка перемычки термостата
        result = await CheckThermostatJumperAsync(context, ct);
        if (!result.Success) return result;

        logger.LogInformation("Все параметры успешно прочитаны и верифицированы");
        return TestStepResult.Pass();
    }

    /// <summary>
    /// Определяет, является ли котёл двухконтурным.
    /// </summary>
    private static bool IsDualCircuit(TestStepContext context)
    {
        var contours = context.RecipeProvider.GetValue<ushort>(NumberOfContoursRecipe)!.Value;
        return contours == 2;
    }

    /// <summary>
    /// Очищает предыдущие результаты для поддержки Retry.
    /// </summary>
    private void ClearPreviousResults()
    {
        testResultsService.Remove(ArticleResultName);
        testResultsService.Remove(BoilerTypeResultName);
        testResultsService.Remove(PumpTypeResultName);
        testResultsService.Remove(PresSensorResultName);
        testResultsService.Remove(GasValveTypeResultName);
        testResultsService.Remove(MaxChHeatOutResultName);
        testResultsService.Remove(MaxDhwHeatOutResultName);
        testResultsService.Remove(MinChHeatOutResultName);
        testResultsService.Remove(PumpModeResultName);
        testResultsService.Remove(PumpPowerResultName);
        testResultsService.Remove(GasTypeResultName);
        testResultsService.Remove(CurrentOffsetResultName);
        testResultsService.Remove(FlowCoefficientResultName);
        testResultsService.Remove(MaxPumpAutoPowerResultName);
        testResultsService.Remove(MinPumpAutoPowerResultName);
        testResultsService.Remove(ComfortHysteresisResultName);
        testResultsService.Remove(MaxFlowTemperatureResultName);
        testResultsService.Remove(ItelmaArticleResultName);
        testResultsService.Remove(ProductionDateResultName);
        testResultsService.Remove(SupplierCodeResultName);
        testResultsService.Remove(CounterNumberResultName);
    }
}

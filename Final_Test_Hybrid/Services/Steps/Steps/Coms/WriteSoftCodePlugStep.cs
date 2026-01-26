using Final_Test_Hybrid.Models;
using Final_Test_Hybrid.Models.Errors;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Services.Steps.Steps.Coms;

/// <summary>
/// Тестовый шаг записи параметров SoftCodePlug в котёл через Modbus.
/// </summary>
public partial class WriteSoftCodePlugStep(
    BoilerState boilerState,
    IOptions<DiagnosticSettings> settings,
    DualLogger<WriteSoftCodePlugStep> logger) : ITestStep, IRequiresRecipes
{
    // Регистры (адреса из документации)
    private const ushort RegisterBoilerArticle = 1175;
    private const ushort RegisterBoilerPowerType1 = 1147;
    private const ushort RegisterBoilerPowerType2 = 1148;
    private const ushort RegisterPumpType1 = 1149;
    private const ushort RegisterPumpType2 = 1150;
    private const ushort RegisterPressureDevice1 = 1151;
    private const ushort RegisterPressureDevice2 = 1152;
    private const ushort RegisterGasRegulator1 = 1158;
    private const ushort RegisterGasRegulator2 = 1159;
    private const ushort RegisterMaxHeatOutputCh = 1050;
    private const ushort RegisterMaxHeatOutputDhw = 1051;
    private const ushort RegisterMinHeatOutputCh = 1053;
    private const ushort RegisterPumpMode = 1108;
    private const ushort RegisterPumpSpeed = 1109;
    private const ushort RegisterGasType = 1065;
    private const ushort RegisterCurrentOffset = 1030;
    private const ushort RegisterFlowCoefficientHi = 1171;
    private const ushort RegisterPumpPowerMax = 1161;
    private const ushort RegisterPumpPowerMin = 1160;
    private const ushort RegisterComfortHysteresis = 1031;
    private const ushort RegisterMaxFlowTemperature = 1052;
    private const int ArticleMaxLength = 14;

    // Рецепты (простые ключи — Coms шаг без PLC)
    private const string NumberOfContoursRecipe = "NumberOfContours";
    private const string BoilerTypeRecipe = "Boiler_Type";
    private const string PumpPowerRecipe = "Pump_Power";
    private const string PumpTypeRecipe = "Pump_Type";
    private const string PresSenTypeRecipe = "Pres_Sen_Type";
    private const string GasValveTypeRecipe = "Gas_Valve_Type";
    private const string MaxChHeatOutMaxRecipe = "Max_CH_HeatOut_Max";
    private const string MaxDhwHeatOutMaxRecipe = "Max_DHW_HeatOut_Max";
    private const string MinChHeatOutMinRecipe = "Min_CH_HeatOut_Min";
    private const string PumpModeRecipe = "Pump_Mode";
    private const string GasTypeRecipe = "Gas_Type";
    private const string CurrentOffsetRecipe = "Current_Offset";
    private const string FlowCoefficientRecipe = "Flow_Coefficient";
    private const string PumpPowerMaxRecipe = "Pump_Power_Max";
    private const string PumpPowerMinRecipe = "Pump_Power_Min";
    private const string ComfortHysteresisRecipe = "Comfort_Hysteresis";
    private const string MaxFlowTemperatureRecipe = "Max_Flow_Temperature";

    private readonly DiagnosticSettings _settings = settings.Value;

    public string Id => "coms-write-soft-code-plug";
    public string Name => "Coms/Write_Soft_Code_Plug";
    public string Description => "Запись параметров в котел";
    public IReadOnlyList<string> RequiredRecipeAddresses =>
    [
        NumberOfContoursRecipe, BoilerTypeRecipe, PumpPowerRecipe, PumpTypeRecipe, PresSenTypeRecipe,
        GasValveTypeRecipe, MaxChHeatOutMaxRecipe, MaxDhwHeatOutMaxRecipe, MinChHeatOutMinRecipe,
        PumpModeRecipe, GasTypeRecipe, CurrentOffsetRecipe, FlowCoefficientRecipe,
        PumpPowerMaxRecipe, PumpPowerMinRecipe, ComfortHysteresisRecipe, MaxFlowTemperatureRecipe
    ];

    /// <summary>
    /// Выполняет запись всех параметров SoftCodePlug в ЭБУ котла.
    /// </summary>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        var isDualCircuit = IsDualCircuit(context);

        // Подшаг 1: Артикул
        var result = await WriteBoilerArticleAsync(context, ct);
        if (!result.Success) return result;

        // Подшаги 2-5: Скрытые регистры
        result = await WriteBoilerPowerTypeAsync(context, ct);
        if (!result.Success) return result;

        result = await WritePumpTypeAsync(context, ct);
        if (!result.Success) return result;

        result = await WritePressureDeviceTypeAsync(context, ct);
        if (!result.Success) return result;

        result = await WriteGasRegulatorTypeAsync(context, ct);
        if (!result.Success) return result;

        // Подшаги 6-17: Обычные регистры
        result = await WriteMaxHeatOutputChAsync(context, ct);
        if (!result.Success) return result;

        result = await WriteMaxHeatOutputDhwAsync(context, ct);
        if (!result.Success) return result;

        result = await WriteMinHeatOutputChAsync(context, ct);
        if (!result.Success) return result;

        result = await WritePumpModeAsync(context, ct);
        if (!result.Success) return result;

        result = await WritePumpSpeedAsync(context, ct);
        if (!result.Success) return result;

        result = await WriteGasTypeAsync(context, ct);
        if (!result.Success) return result;

        result = await WriteCurrentOffsetAsync(context, ct);
        if (!result.Success) return result;

        // Подшаг 13: Только для двухконтурных
        if (isDualCircuit)
        {
            result = await WriteFlowCoefficientAsync(context, ct);
            if (!result.Success) return result;
        }
        else
        {
            logger.LogInformation("Пропуск записи коэффициента k — одноконтурный котёл");
        }

        result = await WritePumpPowerMaxAsync(context, ct);
        if (!result.Success) return result;

        result = await WritePumpPowerMinAsync(context, ct);
        if (!result.Success) return result;

        // Подшаг 16: Только для двухконтурных
        if (isDualCircuit)
        {
            result = await WriteComfortHysteresisAsync(context, ct);
            if (!result.Success) return result;
        }
        else
        {
            logger.LogInformation("Пропуск записи гистерезиса ГВС — одноконтурный котёл");
        }

        result = await WriteMaxFlowTemperatureAsync(context, ct);
        if (!result.Success) return result;

        return TestStepResult.Pass();
    }

    /// <summary>
    /// Проверяет, является ли котёл двухконтурным.
    /// </summary>
    private static bool IsDualCircuit(TestStepContext context)
    {
        var contours = context.RecipeProvider.GetValue<ushort>(NumberOfContoursRecipe)!.Value;
        return contours == 2;
    }

    /// <summary>
    /// Записывает артикульный номер котла в регистры 1175-1181.
    /// </summary>
    private async Task<TestStepResult> WriteBoilerArticleAsync(TestStepContext context, CancellationToken ct)
    {
        var article = boilerState.Article;
        if (string.IsNullOrEmpty(article))
        {
            var msg = $"Ошибка при записи артикула в регистры {RegisterBoilerArticle}-{RegisterBoilerArticle + 6}. Артикул не задан";
            logger.LogError(msg);
            return TestStepResult.Fail(msg, errors: [ErrorDefinitions.EcuWriteError]);
        }

        logger.LogInformation("Запись артикульного номера котла: {Article}", article);

        var address = (ushort)(RegisterBoilerArticle - _settings.BaseAddressOffset);
        var result = await context.DiagWriter.WriteStringAsync(address, article, ArticleMaxLength, ct);

        if (!result.Success)
        {
            var msg = $"Ошибка при записи артикула в регистры {RegisterBoilerArticle}-{RegisterBoilerArticle + 6}. {result.Error}";
            logger.LogError(msg);
            return TestStepResult.Fail(msg, errors: [ErrorDefinitions.EcuWriteError]);
        }

        logger.LogInformation("Артикул записан успешно");
        return TestStepResult.Pass();
    }

    /// <summary>
    /// Записывает одинаковое значение в два скрытых регистра.
    /// </summary>
    private async Task<TestStepResult> WriteDualRegisterAsync(
        TestStepContext context,
        ushort register1,
        ushort register2,
        ushort value,
        string parameterName,
        CancellationToken ct)
    {
        var address1 = (ushort)(register1 - _settings.BaseAddressOffset);
        var address2 = (ushort)(register2 - _settings.BaseAddressOffset);

        var result1 = await context.DiagWriter.WriteUInt16Async(address1, value, ct);
        if (!result1.Success)
        {
            var msg = $"Ошибка при записи {parameterName} в регистры {register1}-{register2}. {result1.Error}";
            logger.LogError(msg);
            return TestStepResult.Fail(msg, errors: [ErrorDefinitions.EcuWriteError]);
        }

        var result2 = await context.DiagWriter.WriteUInt16Async(address2, value, ct);
        if (!result2.Success)
        {
            var msg = $"Ошибка при записи {parameterName} в регистры {register1}-{register2}. {result2.Error}";
            logger.LogError(msg);
            return TestStepResult.Fail(msg, errors: [ErrorDefinitions.EcuWriteError]);
        }

        logger.LogInformation("{Parameter} записан успешно", parameterName);
        return TestStepResult.Pass();
    }
}

using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.Coms;

/// <summary>
/// Методы записи в скрытые регистры (подшаги 2-5).
/// </summary>
public partial class WriteSoftCodePlugStep
{
    /// <summary>
    /// Записывает тип котла в скрытые регистры 1147 и 1148.
    /// </summary>
    private async Task<TestStepResult> WriteBoilerPowerTypeAsync(TestStepContext context, CancellationToken ct)
    {
        var boilerType = context.RecipeProvider.GetValue<ushort>(BoilerTypeRecipe)!.Value;
        logger.LogInformation("Запись типа котла из рецепта Boiler_Type: {Value}", boilerType);

        return await WriteDualRegisterAsync(
            context,
            RegisterBoilerPowerType1,
            RegisterBoilerPowerType2,
            boilerType,
            "типа котла",
            ct);
    }

    /// <summary>
    /// Записывает тип насоса в скрытые регистры 1149 и 1150.
    /// </summary>
    private async Task<TestStepResult> WritePumpTypeAsync(TestStepContext context, CancellationToken ct)
    {
        var pumpType = context.RecipeProvider.GetValue<ushort>(PumpTypeRecipe)!.Value;
        logger.LogInformation("Запись типа насоса из рецепта Pump_Type: {Value}", pumpType);

        return await WriteDualRegisterAsync(
            context,
            RegisterPumpType1,
            RegisterPumpType2,
            pumpType,
            "типа насоса",
            ct);
    }

    /// <summary>
    /// Записывает тип устройства контроля давления в скрытые регистры 1151 и 1152.
    /// </summary>
    private async Task<TestStepResult> WritePressureDeviceTypeAsync(TestStepContext context, CancellationToken ct)
    {
        var pressureDeviceType = context.RecipeProvider.GetValue<ushort>(PresSenTypeRecipe)!.Value;
        logger.LogInformation("Запись типа устройства контроля давления из рецепта Pres_Sen_Type: {Value}", pressureDeviceType);

        return await WriteDualRegisterAsync(
            context,
            RegisterPressureDevice1,
            RegisterPressureDevice2,
            pressureDeviceType,
            "типа устройства контроля давления",
            ct);
    }

    /// <summary>
    /// Записывает тип регулятора давления газа в скрытые регистры 1158 и 1159.
    /// </summary>
    private async Task<TestStepResult> WriteGasRegulatorTypeAsync(TestStepContext context, CancellationToken ct)
    {
        var gasValveType = context.RecipeProvider.GetValue<ushort>(GasValveTypeRecipe)!.Value;
        logger.LogInformation("Запись типа регулятора давления газа из рецепта Gas_Valve_Type: {Value}", gasValveType);

        return await WriteDualRegisterAsync(
            context,
            RegisterGasRegulator1,
            RegisterGasRegulator2,
            gasValveType,
            "типа регулятора давления газа",
            ct);
    }
}

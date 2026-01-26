using Final_Test_Hybrid.Models.Errors;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.Coms;

/// <summary>
/// Методы записи обычных параметров (подшаги 6-17).
/// </summary>
public partial class WriteSoftCodePlugStep
{
    /// <summary>
    /// Записывает максимальную теплопроизводительность отопления в регистр 1050.
    /// </summary>
    private async Task<TestStepResult> WriteMaxHeatOutputChAsync(TestStepContext context, CancellationToken ct)
    {
        var maxHeatOutputCh = context.RecipeProvider.GetValue<ushort>(MaxChHeatOutMaxRecipe)!.Value;
        logger.LogInformation("Запись макс. теплопроизводительности отопления: {Value}%", maxHeatOutputCh);

        var address = (ushort)(RegisterMaxHeatOutputCh - _settings.BaseAddressOffset);
        var result = await context.DiagWriter.WriteUInt16Async(address, maxHeatOutputCh, ct);

        if (!result.Success)
        {
            var msg = $"Ошибка при записи теплопроизводительности отопления в регистр {RegisterMaxHeatOutputCh}. {result.Error}";
            logger.LogError(msg);
            return TestStepResult.Fail(msg, errors: [ErrorDefinitions.EcuWriteError]);
        }

        logger.LogInformation("Макс. теплопроизводительность отопления записана успешно");
        return TestStepResult.Pass();
    }

    /// <summary>
    /// Записывает максимальную теплопроизводительность ГВС в регистр 1051.
    /// </summary>
    private async Task<TestStepResult> WriteMaxHeatOutputDhwAsync(TestStepContext context, CancellationToken ct)
    {
        var maxHeatOutputDhw = context.RecipeProvider.GetValue<ushort>(MaxDhwHeatOutMaxRecipe)!.Value;
        logger.LogInformation("Запись макс. теплопроизводительности ГВС: {Value}%", maxHeatOutputDhw);

        var address = (ushort)(RegisterMaxHeatOutputDhw - _settings.BaseAddressOffset);
        var result = await context.DiagWriter.WriteUInt16Async(address, maxHeatOutputDhw, ct);

        if (!result.Success)
        {
            var msg = $"Ошибка при записи теплопроизводительности ГВС в регистр {RegisterMaxHeatOutputDhw}. {result.Error}";
            logger.LogError(msg);
            return TestStepResult.Fail(msg, errors: [ErrorDefinitions.EcuWriteError]);
        }

        logger.LogInformation("Теплопроизводительность ГВС записана успешно");
        return TestStepResult.Pass();
    }

    /// <summary>
    /// Записывает минимальную теплопроизводительность отопления в регистр 1053.
    /// </summary>
    private async Task<TestStepResult> WriteMinHeatOutputChAsync(TestStepContext context, CancellationToken ct)
    {
        var minHeatOutputCh = context.RecipeProvider.GetValue<ushort>(MinChHeatOutMinRecipe)!.Value;
        logger.LogInformation("Запись мин. теплопроизводительности отопления: {Value}%", minHeatOutputCh);

        var address = (ushort)(RegisterMinHeatOutputCh - _settings.BaseAddressOffset);
        var result = await context.DiagWriter.WriteUInt16Async(address, minHeatOutputCh, ct);

        if (!result.Success)
        {
            var msg = $"Ошибка при записи мин. теплопроизводительности отопления в регистр {RegisterMinHeatOutputCh}. {result.Error}";
            logger.LogError(msg);
            return TestStepResult.Fail(msg, errors: [ErrorDefinitions.EcuWriteError]);
        }

        logger.LogInformation("Мин. теплопроизводительность отопления записана успешно");
        return TestStepResult.Pass();
    }

    /// <summary>
    /// Записывает режим работы насоса в регистр 1108.
    /// </summary>
    private async Task<TestStepResult> WritePumpModeAsync(TestStepContext context, CancellationToken ct)
    {
        var pumpMode = context.RecipeProvider.GetValue<ushort>(PumpModeRecipe)!.Value;
        logger.LogInformation("Запись режима работы насоса: {Value}", pumpMode);

        var address = (ushort)(RegisterPumpMode - _settings.BaseAddressOffset);
        var result = await context.DiagWriter.WriteUInt16Async(address, pumpMode, ct);

        if (!result.Success)
        {
            var msg = $"Ошибка при записи режима работы насоса в регистр {RegisterPumpMode}. {result.Error}";
            logger.LogError(msg);
            return TestStepResult.Fail(msg, errors: [ErrorDefinitions.EcuWriteError]);
        }

        logger.LogInformation("Режим работы насоса записан успешно");
        return TestStepResult.Pass();
    }

    /// <summary>
    /// Записывает установленную скорость насоса в регистр 1109.
    /// </summary>
    private async Task<TestStepResult> WritePumpSpeedAsync(TestStepContext context, CancellationToken ct)
    {
        var pumpSpeed = context.RecipeProvider.GetValue<ushort>(PumpPowerRecipe)!.Value;
        logger.LogInformation("Запись установленной скорости насоса: {Value} Вт", pumpSpeed);

        var address = (ushort)(RegisterPumpSpeed - _settings.BaseAddressOffset);
        var result = await context.DiagWriter.WriteUInt16Async(address, pumpSpeed, ct);

        if (!result.Success)
        {
            var msg = $"Ошибка при записи скорости насоса в регистр {RegisterPumpSpeed}. {result.Error}";
            logger.LogError(msg);
            return TestStepResult.Fail(msg, errors: [ErrorDefinitions.EcuWriteError]);
        }

        logger.LogInformation("Скорость насоса записана успешно");
        return TestStepResult.Pass();
    }

    /// <summary>
    /// Записывает вид подаваемого газа в регистр 1065.
    /// </summary>
    private async Task<TestStepResult> WriteGasTypeAsync(TestStepContext context, CancellationToken ct)
    {
        var gasType = context.RecipeProvider.GetValue<ushort>(GasTypeRecipe)!.Value;
        logger.LogInformation("Запись вида подаваемого газа: {Value}", gasType);

        var address = (ushort)(RegisterGasType - _settings.BaseAddressOffset);
        var result = await context.DiagWriter.WriteUInt16Async(address, gasType, ct);

        if (!result.Success)
        {
            var msg = $"Ошибка при записи вида газа в регистр {RegisterGasType}. {result.Error}";
            logger.LogError(msg);
            return TestStepResult.Fail(msg, errors: [ErrorDefinitions.EcuWriteError]);
        }

        logger.LogInformation("Вид подаваемого газа записан успешно");
        return TestStepResult.Pass();
    }

    /// <summary>
    /// Записывает сдвиг тока на модуляционной катушке в регистр 1030.
    /// </summary>
    private async Task<TestStepResult> WriteCurrentOffsetAsync(TestStepContext context, CancellationToken ct)
    {
        var currentOffset = context.RecipeProvider.GetValue<ushort>(CurrentOffsetRecipe)!.Value;
        logger.LogInformation("Запись сдвига тока на модуляционной катушке: {Value} мА", currentOffset);

        var address = (ushort)(RegisterCurrentOffset - _settings.BaseAddressOffset);
        var result = await context.DiagWriter.WriteUInt16Async(address, currentOffset, ct);

        if (!result.Success)
        {
            var msg = $"Ошибка при записи сдвига тока в регистр {RegisterCurrentOffset}. {result.Error}";
            logger.LogError(msg);
            return TestStepResult.Fail(msg, errors: [ErrorDefinitions.EcuWriteError]);
        }

        logger.LogInformation("Сдвиг тока записан успешно");
        return TestStepResult.Pass();
    }

    /// <summary>
    /// Записывает коэффициент k расхода воды в регистры 1171-1172 (только для двухконтурных).
    /// </summary>
    private async Task<TestStepResult> WriteFlowCoefficientAsync(TestStepContext context, CancellationToken ct)
    {
        var flowCoeff = context.RecipeProvider.GetValue<float>(FlowCoefficientRecipe)!.Value;
        logger.LogInformation("Запись коэффициента k расхода воды: {Value}", flowCoeff);

        var address = (ushort)(RegisterFlowCoefficientHi - _settings.BaseAddressOffset);
        var result = await context.DiagWriter.WriteFloatAsync(address, flowCoeff, ct);

        if (!result.Success)
        {
            var msg = $"Ошибка при записи коэффициента k в регистры {RegisterFlowCoefficientHi}-{RegisterFlowCoefficientHi + 1}. {result.Error}";
            logger.LogError(msg);
            return TestStepResult.Fail(msg, errors: [ErrorDefinitions.EcuWriteError]);
        }

        logger.LogInformation("Коэффициент k записан успешно");
        return TestStepResult.Pass();
    }

    /// <summary>
    /// Записывает максимальную мощность насоса в авто режиме в регистр 1161.
    /// </summary>
    private async Task<TestStepResult> WritePumpPowerMaxAsync(TestStepContext context, CancellationToken ct)
    {
        var pumpPowerMax = context.RecipeProvider.GetValue<ushort>(PumpPowerMaxRecipe)!.Value;
        logger.LogInformation("Запись макс. мощности насоса в авто режиме: {Value} Вт", pumpPowerMax);

        var address = (ushort)(RegisterPumpPowerMax - _settings.BaseAddressOffset);
        var result = await context.DiagWriter.WriteUInt16Async(address, pumpPowerMax, ct);

        if (!result.Success)
        {
            var msg = $"Ошибка при записи макс. мощности насоса в регистр {RegisterPumpPowerMax}. {result.Error}";
            logger.LogError(msg);
            return TestStepResult.Fail(msg, errors: [ErrorDefinitions.EcuWriteError]);
        }

        logger.LogInformation("Макс. мощность насоса записана успешно");
        return TestStepResult.Pass();
    }

    /// <summary>
    /// Записывает минимальную мощность насоса в авто режиме в регистр 1160.
    /// </summary>
    private async Task<TestStepResult> WritePumpPowerMinAsync(TestStepContext context, CancellationToken ct)
    {
        var pumpPowerMin = context.RecipeProvider.GetValue<ushort>(PumpPowerMinRecipe)!.Value;
        logger.LogInformation("Запись мин. мощности насоса в авто режиме: {Value} Вт", pumpPowerMin);

        var address = (ushort)(RegisterPumpPowerMin - _settings.BaseAddressOffset);
        var result = await context.DiagWriter.WriteUInt16Async(address, pumpPowerMin, ct);

        if (!result.Success)
        {
            var msg = $"Ошибка при записи мин. мощности насоса в регистр {RegisterPumpPowerMin}. {result.Error}";
            logger.LogError(msg);
            return TestStepResult.Fail(msg, errors: [ErrorDefinitions.EcuWriteError]);
        }

        logger.LogInformation("Мин. мощность насоса записана успешно");
        return TestStepResult.Pass();
    }

    /// <summary>
    /// Записывает гистерезис подогрева ГВС в регистр 1031 (только для двухконтурных).
    /// </summary>
    private async Task<TestStepResult> WriteComfortHysteresisAsync(TestStepContext context, CancellationToken ct)
    {
        var hysteresis = context.RecipeProvider.GetValue<ushort>(ComfortHysteresisRecipe)!.Value;
        logger.LogInformation("Запись гистерезиса подогрева ГВС: {Value} °С", hysteresis);

        var address = (ushort)(RegisterComfortHysteresis - _settings.BaseAddressOffset);
        var result = await context.DiagWriter.WriteUInt16Async(address, hysteresis, ct);

        if (!result.Success)
        {
            var msg = $"Ошибка при записи гистерезиса ГВС в регистр {RegisterComfortHysteresis}. {result.Error}";
            logger.LogError(msg);
            return TestStepResult.Fail(msg, errors: [ErrorDefinitions.EcuWriteError]);
        }

        logger.LogInformation("Гистерезис подогрева ГВС записан успешно");
        return TestStepResult.Pass();
    }

    /// <summary>
    /// Записывает максимальную температуру подающей линии в регистр 1052.
    /// </summary>
    private async Task<TestStepResult> WriteMaxFlowTemperatureAsync(TestStepContext context, CancellationToken ct)
    {
        var maxFlowTemp = context.RecipeProvider.GetValue<ushort>(MaxFlowTemperatureRecipe)!.Value;
        logger.LogInformation("Запись макс. температуры подающей линии: {Value} °С", maxFlowTemp);

        var address = (ushort)(RegisterMaxFlowTemperature - _settings.BaseAddressOffset);
        var result = await context.DiagWriter.WriteUInt16Async(address, maxFlowTemp, ct);

        if (!result.Success)
        {
            var msg = $"Ошибка при записи макс. температуры подающей линии в регистр {RegisterMaxFlowTemperature}. {result.Error}";
            logger.LogError(msg);
            return TestStepResult.Fail(msg, errors: [ErrorDefinitions.EcuWriteError]);
        }

        logger.LogInformation("Макс. температура подающей линии записана успешно");
        return TestStepResult.Pass();
    }
}

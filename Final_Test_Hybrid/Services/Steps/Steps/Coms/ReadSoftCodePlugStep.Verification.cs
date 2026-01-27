using Final_Test_Hybrid.Models.Errors;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.Coms;

/// <summary>
/// Методы чтения и верификации параметров (подшаги 1-17).
/// </summary>
public partial class ReadSoftCodePlugStep
{
    /// <summary>
    /// Читает и верифицирует артикул из регистров 1139-1145.
    /// </summary>
    private async Task<TestStepResult> ReadAndVerifyArticleAsync(TestStepContext context, CancellationToken ct)
    {
        var expectedArticle = boilerState.Article;
        if (string.IsNullOrEmpty(expectedArticle))
        {
            const string msg = "Артикул не задан в BoilerState";
            logger.LogError(msg);
            return TestStepResult.Fail(msg, errors: [ErrorDefinitions.EcuArticleMismatch]);
        }

        logger.LogInformation("Чтение артикула из регистров {Start}-{End}", RegisterNomenclatureNumber, RegisterNomenclatureNumber + 6);

        var address = (ushort)(RegisterNomenclatureNumber - _settings.BaseAddressOffset);
        var result = await context.DiagReader.ReadStringAsync(address, NomenclatureMaxLength, ct);

        if (!result.Success)
        {
            var msg = $"Ошибка при чтении артикула из регистров {RegisterNomenclatureNumber}-{RegisterNomenclatureNumber + 6}. {result.Error}";
            logger.LogError(msg);
            return TestStepResult.Fail(msg);
        }

        var actualArticle = result.Value;
        var isMatch = actualArticle == expectedArticle;

        testResultsService.Add(
            parameterName: ArticleResultName,
            value: actualArticle ?? "",
            min: "",
            max: "",
            status: isMatch ? 1 : 0,
            isRanged: true,
            unit: "");

        logger.LogInformation("Артикул: {Actual}, ожидался: {Expected}, статус: {Status}",
            actualArticle, expectedArticle, isMatch ? "OK" : "NOK");

        if (!isMatch)
        {
            var msg = $"Артикул в ЭБУ ({actualArticle}) не совпадает с ожидаемым ({expectedArticle})";
            logger.LogError(msg);
            return TestStepResult.Fail(msg, errors: [ErrorDefinitions.EcuArticleMismatch]);
        }

        return TestStepResult.Pass();
    }

    /// <summary>
    /// Читает и верифицирует тип котла из регистра 1002.
    /// </summary>
    private async Task<TestStepResult> ReadAndVerifyBoilerTypeAsync(TestStepContext context, CancellationToken ct)
    {
        var expectedValue = context.RecipeProvider.GetValue<ushort>(BoilerTypeRecipe)!.Value;
        var minValue = context.RecipeProvider.GetValue<ushort>(BoilerTypeMinRecipe)!.Value;
        var maxValue = context.RecipeProvider.GetValue<ushort>(BoilerTypeMaxRecipe)!.Value;

        logger.LogInformation("Чтение типа котла из регистра {Register}", RegisterBoilerPowerType);

        var address = (ushort)(RegisterBoilerPowerType - _settings.BaseAddressOffset);
        var result = await context.DiagReader.ReadUInt16Async(address, ct);

        if (!result.Success)
        {
            var msg = $"Ошибка при чтении типа котла из регистра {RegisterBoilerPowerType}. {result.Error}";
            logger.LogError(msg);
            return TestStepResult.Fail(msg);
        }

        var actualValue = result.Value;
        var isMatch = actualValue == expectedValue;

        testResultsService.Add(
            parameterName: BoilerTypeResultName,
            value: actualValue.ToString(),
            min: minValue.ToString(),
            max: maxValue.ToString(),
            status: isMatch ? 1 : 0,
            isRanged: true,
            unit: "");

        logger.LogInformation("Тип котла: {Actual}, ожидался: {Expected}, статус: {Status}",
            actualValue, expectedValue, isMatch ? "OK" : "NOK");

        if (!isMatch)
        {
            var msg = $"Тип котла в ЭБУ ({actualValue}) не совпадает с ожидаемым ({expectedValue})";
            logger.LogError(msg);
            return TestStepResult.Fail(msg, errors: [ErrorDefinitions.EcuBoilerTypeMismatch]);
        }

        return TestStepResult.Pass();
    }

    /// <summary>
    /// Читает и верифицирует тип насоса из регистра 1003.
    /// </summary>
    private async Task<TestStepResult> ReadAndVerifyPumpTypeAsync(TestStepContext context, CancellationToken ct)
    {
        var expectedValue = context.RecipeProvider.GetValue<ushort>(PumpTypeRecipe)!.Value;
        var minValue = context.RecipeProvider.GetValue<ushort>(PumpTypeMinRecipe)!.Value;
        var maxValue = context.RecipeProvider.GetValue<ushort>(PumpTypeMaxRecipe)!.Value;

        logger.LogInformation("Чтение типа насоса из регистра {Register}", RegisterPumpType);

        var address = (ushort)(RegisterPumpType - _settings.BaseAddressOffset);
        var result = await context.DiagReader.ReadUInt16Async(address, ct);

        if (!result.Success)
        {
            var msg = $"Ошибка при чтении типа насоса из регистра {RegisterPumpType}. {result.Error}";
            logger.LogError(msg);
            return TestStepResult.Fail(msg);
        }

        var actualValue = result.Value;
        var isMatch = actualValue == expectedValue;

        testResultsService.Add(
            parameterName: PumpTypeResultName,
            value: actualValue.ToString(),
            min: minValue.ToString(),
            max: maxValue.ToString(),
            status: isMatch ? 1 : 0,
            isRanged: true,
            unit: "");

        logger.LogInformation("Тип насоса: {Actual}, ожидался: {Expected}, статус: {Status}",
            actualValue, expectedValue, isMatch ? "OK" : "NOK");

        if (!isMatch)
        {
            var msg = $"Тип насоса в ЭБУ ({actualValue}) не совпадает с ожидаемым ({expectedValue})";
            logger.LogError(msg);
            return TestStepResult.Fail(msg, errors: [ErrorDefinitions.EcuPumpTypeMismatch]);
        }

        return TestStepResult.Pass();
    }

    /// <summary>
    /// Читает и верифицирует тип датчика давления из регистра 1004.
    /// </summary>
    private async Task<TestStepResult> ReadAndVerifyPressureDeviceTypeAsync(TestStepContext context, CancellationToken ct)
    {
        var expectedValue = context.RecipeProvider.GetValue<ushort>(PresSenTypeRecipe)!.Value;
        var minValue = context.RecipeProvider.GetValue<ushort>(PresSensorMinRecipe)!.Value;
        var maxValue = context.RecipeProvider.GetValue<ushort>(PresSensorMaxRecipe)!.Value;

        logger.LogInformation("Чтение типа датчика давления из регистра {Register}", RegisterPressureDeviceType);

        var address = (ushort)(RegisterPressureDeviceType - _settings.BaseAddressOffset);
        var result = await context.DiagReader.ReadUInt16Async(address, ct);

        if (!result.Success)
        {
            var msg = $"Ошибка при чтении типа датчика давления из регистра {RegisterPressureDeviceType}. {result.Error}";
            logger.LogError(msg);
            return TestStepResult.Fail(msg);
        }

        var actualValue = result.Value;
        var isMatch = actualValue == expectedValue;

        testResultsService.Add(
            parameterName: PresSensorResultName,
            value: actualValue.ToString(),
            min: minValue.ToString(),
            max: maxValue.ToString(),
            status: isMatch ? 1 : 0,
            isRanged: true,
            unit: "");

        logger.LogInformation("Тип датчика давления: {Actual}, ожидался: {Expected}, статус: {Status}",
            actualValue, expectedValue, isMatch ? "OK" : "NOK");

        if (!isMatch)
        {
            var msg = $"Тип датчика давления в ЭБУ ({actualValue}) не совпадает с ожидаемым ({expectedValue})";
            logger.LogError(msg);
            return TestStepResult.Fail(msg, errors: [ErrorDefinitions.EcuPressureDeviceTypeMismatch]);
        }

        return TestStepResult.Pass();
    }

    /// <summary>
    /// Читает и верифицирует тип регулятора газа из регистра 1157.
    /// </summary>
    private async Task<TestStepResult> ReadAndVerifyGasRegulatorTypeAsync(TestStepContext context, CancellationToken ct)
    {
        var expectedValue = context.RecipeProvider.GetValue<ushort>(GasValveTypeRecipe)!.Value;
        var minValue = context.RecipeProvider.GetValue<ushort>(GasValveTypeMinRecipe)!.Value;
        var maxValue = context.RecipeProvider.GetValue<ushort>(GasValveTypeMaxRecipe)!.Value;

        logger.LogInformation("Чтение типа регулятора газа из регистра {Register}", RegisterGasRegulatorType);

        var address = (ushort)(RegisterGasRegulatorType - _settings.BaseAddressOffset);
        var result = await context.DiagReader.ReadUInt16Async(address, ct);

        if (!result.Success)
        {
            var msg = $"Ошибка при чтении типа регулятора газа из регистра {RegisterGasRegulatorType}. {result.Error}";
            logger.LogError(msg);
            return TestStepResult.Fail(msg);
        }

        var actualValue = result.Value;
        var isMatch = actualValue == expectedValue;

        testResultsService.Add(
            parameterName: GasValveTypeResultName,
            value: actualValue.ToString(),
            min: minValue.ToString(),
            max: maxValue.ToString(),
            status: isMatch ? 1 : 0,
            isRanged: true,
            unit: "");

        logger.LogInformation("Тип регулятора газа: {Actual}, ожидался: {Expected}, статус: {Status}",
            actualValue, expectedValue, isMatch ? "OK" : "NOK");

        if (!isMatch)
        {
            var msg = $"Тип регулятора газа в ЭБУ ({actualValue}) не совпадает с ожидаемым ({expectedValue})";
            logger.LogError(msg);
            return TestStepResult.Fail(msg, errors: [ErrorDefinitions.EcuGasRegulatorTypeMismatch]);
        }

        return TestStepResult.Pass();
    }

    /// <summary>
    /// Читает и верифицирует макс. теплопроизводительность отопления из регистра 1050.
    /// </summary>
    private async Task<TestStepResult> ReadAndVerifyMaxChHeatOutputAsync(TestStepContext context, CancellationToken ct)
    {
        var expectedValue = context.RecipeProvider.GetValue<ushort>(MaxChHeatOutRecipe)!.Value;
        var minValue = context.RecipeProvider.GetValue<ushort>(MaxChHeatOutMinRecipe)!.Value;
        var maxValue = context.RecipeProvider.GetValue<ushort>(MaxChHeatOutMaxRecipe)!.Value;

        logger.LogInformation("Чтение макс. теплопроизводительности отопления из регистра {Register}", RegisterMaxHeatOutputCh);

        var address = (ushort)(RegisterMaxHeatOutputCh - _settings.BaseAddressOffset);
        var result = await context.DiagReader.ReadUInt16Async(address, ct);

        if (!result.Success)
        {
            var msg = $"Ошибка при чтении макс. теплопроизводительности отопления из регистра {RegisterMaxHeatOutputCh}. {result.Error}";
            logger.LogError(msg);
            return TestStepResult.Fail(msg);
        }

        var actualValue = result.Value;
        var isMatch = actualValue == expectedValue;

        testResultsService.Add(
            parameterName: MaxChHeatOutResultName,
            value: actualValue.ToString(),
            min: minValue.ToString(),
            max: maxValue.ToString(),
            status: isMatch ? 1 : 0,
            isRanged: true,
            unit: "%");

        logger.LogInformation("Макс. теплопроизводительность отопления: {Actual}%, ожидалось: {Expected}%, статус: {Status}",
            actualValue, expectedValue, isMatch ? "OK" : "NOK");

        if (!isMatch)
        {
            var msg = $"Макс. теплопроизводительность отопления в ЭБУ ({actualValue}%) не совпадает с ожидаемым ({expectedValue}%)";
            logger.LogError(msg);
            return TestStepResult.Fail(msg, errors: [ErrorDefinitions.EcuMaxChHeatOutputMismatch]);
        }

        return TestStepResult.Pass();
    }

    /// <summary>
    /// Читает и верифицирует макс. теплопроизводительность ГВС из регистра 1051.
    /// </summary>
    private async Task<TestStepResult> ReadAndVerifyMaxDhwHeatOutputAsync(TestStepContext context, CancellationToken ct)
    {
        var expectedValue = context.RecipeProvider.GetValue<ushort>(MaxDhwHeatOutRecipe)!.Value;
        var minValue = context.RecipeProvider.GetValue<ushort>(MaxDhwHeatOutMinRecipe)!.Value;
        var maxValue = context.RecipeProvider.GetValue<ushort>(MaxDhwHeatOutMaxRecipe)!.Value;

        logger.LogInformation("Чтение макс. теплопроизводительности ГВС из регистра {Register}", RegisterMaxHeatOutputDhw);

        var address = (ushort)(RegisterMaxHeatOutputDhw - _settings.BaseAddressOffset);
        var result = await context.DiagReader.ReadUInt16Async(address, ct);

        if (!result.Success)
        {
            var msg = $"Ошибка при чтении макс. теплопроизводительности ГВС из регистра {RegisterMaxHeatOutputDhw}. {result.Error}";
            logger.LogError(msg);
            return TestStepResult.Fail(msg);
        }

        var actualValue = result.Value;
        var isMatch = actualValue == expectedValue;

        testResultsService.Add(
            parameterName: MaxDhwHeatOutResultName,
            value: actualValue.ToString(),
            min: minValue.ToString(),
            max: maxValue.ToString(),
            status: isMatch ? 1 : 0,
            isRanged: true,
            unit: "%");

        logger.LogInformation("Макс. теплопроизводительность ГВС: {Actual}%, ожидалось: {Expected}%, статус: {Status}",
            actualValue, expectedValue, isMatch ? "OK" : "NOK");

        if (!isMatch)
        {
            var msg = $"Макс. теплопроизводительность ГВС в ЭБУ ({actualValue}%) не совпадает с ожидаемым ({expectedValue}%)";
            logger.LogError(msg);
            return TestStepResult.Fail(msg, errors: [ErrorDefinitions.EcuMaxDhwHeatOutputMismatch]);
        }

        return TestStepResult.Pass();
    }

    /// <summary>
    /// Читает и верифицирует мин. теплопроизводительность отопления из регистра 1053.
    /// </summary>
    private async Task<TestStepResult> ReadAndVerifyMinChHeatOutputAsync(TestStepContext context, CancellationToken ct)
    {
        var expectedValue = context.RecipeProvider.GetValue<ushort>(MinChHeatOutRecipe)!.Value;
        var minValue = context.RecipeProvider.GetValue<ushort>(MinChHeatOutMinRecipe)!.Value;
        var maxValue = context.RecipeProvider.GetValue<ushort>(MinChHeatOutMaxRecipe)!.Value;

        logger.LogInformation("Чтение мин. теплопроизводительности отопления из регистра {Register}", RegisterMinHeatOutputCh);

        var address = (ushort)(RegisterMinHeatOutputCh - _settings.BaseAddressOffset);
        var result = await context.DiagReader.ReadUInt16Async(address, ct);

        if (!result.Success)
        {
            var msg = $"Ошибка при чтении мин. теплопроизводительности отопления из регистра {RegisterMinHeatOutputCh}. {result.Error}";
            logger.LogError(msg);
            return TestStepResult.Fail(msg);
        }

        var actualValue = result.Value;
        var isMatch = actualValue == expectedValue;

        testResultsService.Add(
            parameterName: MinChHeatOutResultName,
            value: actualValue.ToString(),
            min: minValue.ToString(),
            max: maxValue.ToString(),
            status: isMatch ? 1 : 0,
            isRanged: true,
            unit: "%");

        logger.LogInformation("Мин. теплопроизводительность отопления: {Actual}%, ожидалось: {Expected}%, статус: {Status}",
            actualValue, expectedValue, isMatch ? "OK" : "NOK");

        if (!isMatch)
        {
            var msg = $"Мин. теплопроизводительность отопления в ЭБУ ({actualValue}%) не совпадает с ожидаемым ({expectedValue}%)";
            logger.LogError(msg);
            return TestStepResult.Fail(msg, errors: [ErrorDefinitions.EcuMinChHeatOutputMismatch]);
        }

        return TestStepResult.Pass();
    }

    /// <summary>
    /// Читает и верифицирует режим работы насоса из регистра 1108.
    /// </summary>
    private async Task<TestStepResult> ReadAndVerifyPumpModeAsync(TestStepContext context, CancellationToken ct)
    {
        var expectedValue = context.RecipeProvider.GetValue<ushort>(PumpModeRecipe)!.Value;
        var minValue = context.RecipeProvider.GetValue<ushort>(PumpModeMinRecipe)!.Value;
        var maxValue = context.RecipeProvider.GetValue<ushort>(PumpModeMaxRecipe)!.Value;

        logger.LogInformation("Чтение режима работы насоса из регистра {Register}", RegisterPumpMode);

        var address = (ushort)(RegisterPumpMode - _settings.BaseAddressOffset);
        var result = await context.DiagReader.ReadUInt16Async(address, ct);

        if (!result.Success)
        {
            var msg = $"Ошибка при чтении режима работы насоса из регистра {RegisterPumpMode}. {result.Error}";
            logger.LogError(msg);
            return TestStepResult.Fail(msg);
        }

        var actualValue = result.Value;
        var isMatch = actualValue == expectedValue;

        testResultsService.Add(
            parameterName: PumpModeResultName,
            value: actualValue.ToString(),
            min: minValue.ToString(),
            max: maxValue.ToString(),
            status: isMatch ? 1 : 0,
            isRanged: true,
            unit: "");

        logger.LogInformation("Режим работы насоса: {Actual}, ожидался: {Expected}, статус: {Status}",
            actualValue, expectedValue, isMatch ? "OK" : "NOK");

        if (!isMatch)
        {
            var msg = $"Режим работы насоса в ЭБУ ({actualValue}) не совпадает с ожидаемым ({expectedValue})";
            logger.LogError(msg);
            return TestStepResult.Fail(msg, errors: [ErrorDefinitions.EcuPumpModeMismatch]);
        }

        return TestStepResult.Pass();
    }

    /// <summary>
    /// Читает и верифицирует установленную мощность насоса из регистра 1109.
    /// </summary>
    private async Task<TestStepResult> ReadAndVerifyPumpPowerAsync(TestStepContext context, CancellationToken ct)
    {
        var expectedValue = context.RecipeProvider.GetValue<ushort>(PumpPowerRecipe)!.Value;
        var minValue = context.RecipeProvider.GetValue<ushort>(PumpPowerMinRecipe)!.Value;
        var maxValue = context.RecipeProvider.GetValue<ushort>(PumpPowerMaxRecipe)!.Value;

        logger.LogInformation("Чтение установленной мощности насоса из регистра {Register}", RegisterPumpPower);

        var address = (ushort)(RegisterPumpPower - _settings.BaseAddressOffset);
        var result = await context.DiagReader.ReadUInt16Async(address, ct);

        if (!result.Success)
        {
            var msg = $"Ошибка при чтении установленной мощности насоса из регистра {RegisterPumpPower}. {result.Error}";
            logger.LogError(msg);
            return TestStepResult.Fail(msg);
        }

        var actualValue = result.Value;
        var isMatch = actualValue == expectedValue;

        testResultsService.Add(
            parameterName: PumpPowerResultName,
            value: actualValue.ToString(),
            min: minValue.ToString(),
            max: maxValue.ToString(),
            status: isMatch ? 1 : 0,
            isRanged: true,
            unit: "");

        logger.LogInformation("Установленная мощность насоса: {Actual}, ожидалось: {Expected}, статус: {Status}",
            actualValue, expectedValue, isMatch ? "OK" : "NOK");

        if (!isMatch)
        {
            var msg = $"Установленная мощность насоса в ЭБУ ({actualValue}) не совпадает с ожидаемым ({expectedValue})";
            logger.LogError(msg);
            return TestStepResult.Fail(msg, errors: [ErrorDefinitions.EcuPumpPowerMismatch]);
        }

        return TestStepResult.Pass();
    }

    /// <summary>
    /// Читает и верифицирует вид подаваемого газа из регистра 1065.
    /// </summary>
    private async Task<TestStepResult> ReadAndVerifyGasTypeAsync(TestStepContext context, CancellationToken ct)
    {
        var expectedValue = context.RecipeProvider.GetValue<ushort>(GasTypeRecipe)!.Value;
        var minValue = context.RecipeProvider.GetValue<ushort>(GasTypeMinRecipe)!.Value;
        var maxValue = context.RecipeProvider.GetValue<ushort>(GasTypeMaxRecipe)!.Value;

        logger.LogInformation("Чтение вида подаваемого газа из регистра {Register}", RegisterGasType);

        var address = (ushort)(RegisterGasType - _settings.BaseAddressOffset);
        var result = await context.DiagReader.ReadUInt16Async(address, ct);

        if (!result.Success)
        {
            var msg = $"Ошибка при чтении вида подаваемого газа из регистра {RegisterGasType}. {result.Error}";
            logger.LogError(msg);
            return TestStepResult.Fail(msg);
        }

        var actualValue = result.Value;
        var isMatch = actualValue == expectedValue;

        testResultsService.Add(
            parameterName: GasTypeResultName,
            value: actualValue.ToString(),
            min: minValue.ToString(),
            max: maxValue.ToString(),
            status: isMatch ? 1 : 0,
            isRanged: true,
            unit: "");

        logger.LogInformation("Вид подаваемого газа: {Actual}, ожидался: {Expected}, статус: {Status}",
            actualValue, expectedValue, isMatch ? "OK" : "NOK");

        if (!isMatch)
        {
            var msg = $"Вид подаваемого газа в ЭБУ ({actualValue}) не совпадает с ожидаемым ({expectedValue})";
            logger.LogError(msg);
            return TestStepResult.Fail(msg, errors: [ErrorDefinitions.EcuGasTypeMismatch]);
        }

        return TestStepResult.Pass();
    }

    /// <summary>
    /// Читает и верифицирует сдвиг тока на модуляционной катушке из регистра 1030.
    /// </summary>
    private async Task<TestStepResult> ReadAndVerifyCurrentOffsetAsync(TestStepContext context, CancellationToken ct)
    {
        var expectedValue = context.RecipeProvider.GetValue<ushort>(CurrentOffsetRecipe)!.Value;
        var minValue = context.RecipeProvider.GetValue<ushort>(CurrentOffsetMinRecipe)!.Value;
        var maxValue = context.RecipeProvider.GetValue<ushort>(CurrentOffsetMaxRecipe)!.Value;

        logger.LogInformation("Чтение сдвига тока на модуляционной катушке из регистра {Register}", RegisterCurrentOffset);

        var address = (ushort)(RegisterCurrentOffset - _settings.BaseAddressOffset);
        var result = await context.DiagReader.ReadUInt16Async(address, ct);

        if (!result.Success)
        {
            var msg = $"Ошибка при чтении сдвига тока на модуляционной катушке из регистра {RegisterCurrentOffset}. {result.Error}";
            logger.LogError(msg);
            return TestStepResult.Fail(msg);
        }

        var actualValue = result.Value;
        var isMatch = actualValue == expectedValue;

        testResultsService.Add(
            parameterName: CurrentOffsetResultName,
            value: actualValue.ToString(),
            min: minValue.ToString(),
            max: maxValue.ToString(),
            status: isMatch ? 1 : 0,
            isRanged: true,
            unit: "мА");

        logger.LogInformation("Сдвиг тока: {Actual} мА, ожидалось: {Expected} мА, статус: {Status}",
            actualValue, expectedValue, isMatch ? "OK" : "NOK");

        if (!isMatch)
        {
            var msg = $"Сдвиг тока на модуляционной катушке в ЭБУ ({actualValue} мА) не совпадает с ожидаемым ({expectedValue} мА)";
            logger.LogError(msg);
            return TestStepResult.Fail(msg, errors: [ErrorDefinitions.EcuCurrentOffsetMismatch]);
        }

        return TestStepResult.Pass();
    }

    /// <summary>
    /// Читает и верифицирует коэффициент k расхода воды из регистров 1171-1172.
    /// </summary>
    private async Task<TestStepResult> ReadAndVerifyFlowCoefficientAsync(TestStepContext context, CancellationToken ct)
    {
        var expectedValue = context.RecipeProvider.GetValue<float>(FlowCoefficientRecipe)!.Value;
        var minValue = context.RecipeProvider.GetValue<float>(FlowCoefficientMinRecipe)!.Value;
        var maxValue = context.RecipeProvider.GetValue<float>(FlowCoefficientMaxRecipe)!.Value;

        logger.LogInformation("Чтение коэффициента k расхода воды из регистров {Hi}-{Lo}", RegisterFlowCoefficientHi, RegisterFlowCoefficientHi + 1);

        var address = (ushort)(RegisterFlowCoefficientHi - _settings.BaseAddressOffset);
        var result = await context.DiagReader.ReadFloatAsync(address, ct);

        if (!result.Success)
        {
            var msg = $"Ошибка при чтении коэффициента k расхода воды из регистров {RegisterFlowCoefficientHi}-{RegisterFlowCoefficientHi + 1}. {result.Error}";
            logger.LogError(msg);
            return TestStepResult.Fail(msg);
        }

        var actualValue = result.Value;
        var isMatch = actualValue == expectedValue;

        testResultsService.Add(
            parameterName: FlowCoefficientResultName,
            value: actualValue.ToString("F3"),
            min: minValue.ToString("F3"),
            max: maxValue.ToString("F3"),
            status: isMatch ? 1 : 0,
            isRanged: true,
            unit: "");

        logger.LogInformation("Коэффициент k: {Actual:F3}, ожидалось: {Expected:F3}, статус: {Status}",
            actualValue, expectedValue, isMatch ? "OK" : "NOK");

        if (!isMatch)
        {
            var msg = $"Коэффициент k расхода воды в ЭБУ ({actualValue:F3}) не совпадает с ожидаемым ({expectedValue:F3})";
            logger.LogError(msg);
            return TestStepResult.Fail(msg, errors: [ErrorDefinitions.EcuFlowCoefficientMismatch]);
        }

        return TestStepResult.Pass();
    }

    /// <summary>
    /// Читает и верифицирует макс. мощность насоса в авто режиме из регистра 1161.
    /// </summary>
    private async Task<TestStepResult> ReadAndVerifyMaxPumpAutoPowerAsync(TestStepContext context, CancellationToken ct)
    {
        var expectedValue = context.RecipeProvider.GetValue<ushort>(MaxPumpAutoPowerRecipe)!.Value;
        var minValue = context.RecipeProvider.GetValue<ushort>(MaxPumpAutoPowerMinRecipe)!.Value;
        var maxValue = context.RecipeProvider.GetValue<ushort>(MaxPumpAutoPowerMaxRecipe)!.Value;

        logger.LogInformation("Чтение макс. мощности насоса в авто режиме из регистра {Register}", RegisterPumpPowerMax);

        var address = (ushort)(RegisterPumpPowerMax - _settings.BaseAddressOffset);
        var result = await context.DiagReader.ReadUInt16Async(address, ct);

        if (!result.Success)
        {
            var msg = $"Ошибка при чтении макс. мощности насоса в авто режиме из регистра {RegisterPumpPowerMax}. {result.Error}";
            logger.LogError(msg);
            return TestStepResult.Fail(msg);
        }

        var actualValue = result.Value;
        var isMatch = actualValue == expectedValue;

        testResultsService.Add(
            parameterName: MaxPumpAutoPowerResultName,
            value: actualValue.ToString(),
            min: minValue.ToString(),
            max: maxValue.ToString(),
            status: isMatch ? 1 : 0,
            isRanged: true,
            unit: "Вт");

        logger.LogInformation("Макс. мощность насоса в авто режиме: {Actual} Вт, ожидалось: {Expected} Вт, статус: {Status}",
            actualValue, expectedValue, isMatch ? "OK" : "NOK");

        if (!isMatch)
        {
            var msg = $"Макс. мощность насоса в авто режиме в ЭБУ ({actualValue} Вт) не совпадает с ожидаемым ({expectedValue} Вт)";
            logger.LogError(msg);
            return TestStepResult.Fail(msg, errors: [ErrorDefinitions.EcuMaxPumpAutoPowerMismatch]);
        }

        return TestStepResult.Pass();
    }

    /// <summary>
    /// Читает и верифицирует мин. мощность насоса в авто режиме из регистра 1160.
    /// </summary>
    private async Task<TestStepResult> ReadAndVerifyMinPumpAutoPowerAsync(TestStepContext context, CancellationToken ct)
    {
        var expectedValue = context.RecipeProvider.GetValue<ushort>(MinPumpAutoPowerRecipe)!.Value;
        var minValue = context.RecipeProvider.GetValue<ushort>(MinPumpAutoPowerMinRecipe)!.Value;
        var maxValue = context.RecipeProvider.GetValue<ushort>(MinPumpAutoPowerMaxRecipe)!.Value;

        logger.LogInformation("Чтение мин. мощности насоса в авто режиме из регистра {Register}", RegisterPumpPowerMin);

        var address = (ushort)(RegisterPumpPowerMin - _settings.BaseAddressOffset);
        var result = await context.DiagReader.ReadUInt16Async(address, ct);

        if (!result.Success)
        {
            var msg = $"Ошибка при чтении мин. мощности насоса в авто режиме из регистра {RegisterPumpPowerMin}. {result.Error}";
            logger.LogError(msg);
            return TestStepResult.Fail(msg);
        }

        var actualValue = result.Value;
        var isMatch = actualValue == expectedValue;

        testResultsService.Add(
            parameterName: MinPumpAutoPowerResultName,
            value: actualValue.ToString(),
            min: minValue.ToString(),
            max: maxValue.ToString(),
            status: isMatch ? 1 : 0,
            isRanged: true,
            unit: "Вт");

        logger.LogInformation("Мин. мощность насоса в авто режиме: {Actual} Вт, ожидалось: {Expected} Вт, статус: {Status}",
            actualValue, expectedValue, isMatch ? "OK" : "NOK");

        if (!isMatch)
        {
            var msg = $"Мин. мощность насоса в авто режиме в ЭБУ ({actualValue} Вт) не совпадает с ожидаемым ({expectedValue} Вт)";
            logger.LogError(msg);
            return TestStepResult.Fail(msg, errors: [ErrorDefinitions.EcuMinPumpAutoPowerMismatch]);
        }

        return TestStepResult.Pass();
    }

    /// <summary>
    /// Читает и верифицирует гистерезис ГВС в режиме комфорт из регистра 1031.
    /// </summary>
    private async Task<TestStepResult> ReadAndVerifyComfortHysteresisAsync(TestStepContext context, CancellationToken ct)
    {
        var expectedValue = context.RecipeProvider.GetValue<ushort>(ComfortHysteresisRecipe)!.Value;
        var minValue = context.RecipeProvider.GetValue<ushort>(ComfortHysteresisMinRecipe)!.Value;
        var maxValue = context.RecipeProvider.GetValue<ushort>(ComfortHysteresisMaxRecipe)!.Value;

        logger.LogInformation("Чтение гистерезиса ГВС в режиме комфорт из регистра {Register}", RegisterComfortHysteresis);

        var address = (ushort)(RegisterComfortHysteresis - _settings.BaseAddressOffset);
        var result = await context.DiagReader.ReadUInt16Async(address, ct);

        if (!result.Success)
        {
            var msg = $"Ошибка при чтении гистерезиса ГВС в режиме комфорт из регистра {RegisterComfortHysteresis}. {result.Error}";
            logger.LogError(msg);
            return TestStepResult.Fail(msg);
        }

        var actualValue = result.Value;
        var isMatch = actualValue == expectedValue;

        testResultsService.Add(
            parameterName: ComfortHysteresisResultName,
            value: actualValue.ToString(),
            min: minValue.ToString(),
            max: maxValue.ToString(),
            status: isMatch ? 1 : 0,
            isRanged: true,
            unit: "°С");

        logger.LogInformation("Гистерезис ГВС: {Actual} °С, ожидалось: {Expected} °С, статус: {Status}",
            actualValue, expectedValue, isMatch ? "OK" : "NOK");

        if (!isMatch)
        {
            var msg = $"Гистерезис ГВС в режиме комфорт в ЭБУ ({actualValue} °С) не совпадает с ожидаемым ({expectedValue} °С)";
            logger.LogError(msg);
            return TestStepResult.Fail(msg, errors: [ErrorDefinitions.EcuComfortHysteresisMismatch]);
        }

        return TestStepResult.Pass();
    }

    /// <summary>
    /// Читает и верифицирует макс. температуру подающей линии из регистра 1052.
    /// </summary>
    private async Task<TestStepResult> ReadAndVerifyMaxFlowTemperatureAsync(TestStepContext context, CancellationToken ct)
    {
        var expectedValue = context.RecipeProvider.GetValue<ushort>(MaxFlowTemperatureRecipe)!.Value;
        var minValue = context.RecipeProvider.GetValue<ushort>(MaxFlowTemperatureMinRecipe)!.Value;
        var maxValue = context.RecipeProvider.GetValue<ushort>(MaxFlowTemperatureMaxRecipe)!.Value;

        logger.LogInformation("Чтение макс. температуры подающей линии из регистра {Register}", RegisterMaxFlowTemperature);

        var address = (ushort)(RegisterMaxFlowTemperature - _settings.BaseAddressOffset);
        var result = await context.DiagReader.ReadUInt16Async(address, ct);

        if (!result.Success)
        {
            var msg = $"Ошибка при чтении макс. температуры подающей линии из регистра {RegisterMaxFlowTemperature}. {result.Error}";
            logger.LogError(msg);
            return TestStepResult.Fail(msg);
        }

        var actualValue = result.Value;
        var isMatch = actualValue == expectedValue;

        testResultsService.Add(
            parameterName: MaxFlowTemperatureResultName,
            value: actualValue.ToString(),
            min: minValue.ToString(),
            max: maxValue.ToString(),
            status: isMatch ? 1 : 0,
            isRanged: true,
            unit: "°С");

        logger.LogInformation("Макс. температура подающей линии: {Actual} °С, ожидалось: {Expected} °С, статус: {Status}",
            actualValue, expectedValue, isMatch ? "OK" : "NOK");

        if (!isMatch)
        {
            var msg = $"Макс. температура подающей линии в ЭБУ ({actualValue} °С) не совпадает с ожидаемым ({expectedValue} °С)";
            logger.LogError(msg);
            return TestStepResult.Fail(msg, errors: [ErrorDefinitions.EcuMaxFlowTemperatureMismatch]);
        }

        return TestStepResult.Pass();
    }
}

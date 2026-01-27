using Final_Test_Hybrid.Models.Errors;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.Coms;

/// <summary>
/// Методы чтения параметров без верификации (подшаги 18-22).
/// </summary>
public partial class ReadSoftCodePlugStep
{
    /// <summary>
    /// Читает артикул ИТЭЛМА из регистров 1182-1188 (без верификации).
    /// </summary>
    private async Task<TestStepResult> ReadItelmaArticleAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Чтение артикула ИТЭЛМА из регистров {Start}-{End}", RegisterItelmaArticle, RegisterItelmaArticle + 6);

        var address = (ushort)(RegisterItelmaArticle - _settings.BaseAddressOffset);
        var result = await context.DiagReader.ReadStringAsync(address, NomenclatureMaxLength, ct);

        if (!result.Success)
        {
            var msg = $"Ошибка при чтении артикула ИТЭЛМА из регистров {RegisterItelmaArticle}-{RegisterItelmaArticle + 6}. {result.Error}";
            logger.LogError(msg);
            return TestStepResult.Fail(msg);
        }

        var actualArticle = result.Value;

        testResultsService.Add(
            parameterName: ItelmaArticleResultName,
            value: actualArticle ?? "",
            min: "",
            max: "",
            status: 1,
            isRanged: true,
            unit: "");

        logger.LogInformation("Артикул ИТЭЛМА: {Value}", actualArticle);
        return TestStepResult.Pass();
    }

    /// <summary>
    /// Читает дату производства из регистров 1133-1136 (без верификации).
    /// </summary>
    private async Task<TestStepResult> ReadProductionDateAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Чтение даты производства из регистров {Start}-{End}", RegisterProductionDate, RegisterProductionDate + 3);

        var address = (ushort)(RegisterProductionDate - _settings.BaseAddressOffset);
        var result = await context.DiagReader.ReadStringAsync(address, ProductionDateMaxLength, ct);

        if (!result.Success)
        {
            var msg = $"Ошибка при чтении даты производства из регистров {RegisterProductionDate}-{RegisterProductionDate + 3}. {result.Error}";
            logger.LogError(msg);
            return TestStepResult.Fail(msg);
        }

        var actualDate = result.Value;

        testResultsService.Add(
            parameterName: ProductionDateResultName,
            value: actualDate ?? "",
            min: "",
            max: "",
            status: 1,
            isRanged: true,
            unit: "");

        logger.LogInformation("Дата производства: {Value}", actualDate);
        return TestStepResult.Pass();
    }

    /// <summary>
    /// Читает код поставщика из регистров 1131-1132 (без верификации).
    /// </summary>
    private async Task<TestStepResult> ReadSupplierCodeAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Чтение кода поставщика из регистров {Hi}-{Lo}", RegisterSupplierCodeHi, RegisterSupplierCodeHi + 1);

        var address = (ushort)(RegisterSupplierCodeHi - _settings.BaseAddressOffset);
        var result = await context.DiagReader.ReadUInt32Async(address, ct);

        if (!result.Success)
        {
            var msg = $"Ошибка при чтении кода поставщика из регистров {RegisterSupplierCodeHi}-{RegisterSupplierCodeHi + 1}. {result.Error}";
            logger.LogError(msg);
            return TestStepResult.Fail(msg);
        }

        var actualCode = result.Value;

        testResultsService.Add(
            parameterName: SupplierCodeResultName,
            value: actualCode.ToString(),
            min: "",
            max: "",
            status: 1,
            isRanged: true,
            unit: "");

        logger.LogInformation("Код поставщика: {Value}", actualCode);
        return TestStepResult.Pass();
    }

    /// <summary>
    /// Читает порядковый номер из регистров 1137-1138 (без верификации).
    /// </summary>
    private async Task<TestStepResult> ReadCounterNumberAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Чтение порядкового номера из регистров {Hi}-{Lo}", RegisterCounterNumberHi, RegisterCounterNumberHi + 1);

        var address = (ushort)(RegisterCounterNumberHi - _settings.BaseAddressOffset);
        var result = await context.DiagReader.ReadUInt32Async(address, ct);

        if (!result.Success)
        {
            var msg = $"Ошибка при чтении порядкового номера из регистров {RegisterCounterNumberHi}-{RegisterCounterNumberHi + 1}. {result.Error}";
            logger.LogError(msg);
            return TestStepResult.Fail(msg);
        }

        var actualNumber = result.Value;

        testResultsService.Add(
            parameterName: CounterNumberResultName,
            value: actualNumber.ToString(),
            min: "",
            max: "",
            status: 1,
            isRanged: true,
            unit: "");

        logger.LogInformation("Порядковый номер: {Value}", actualNumber);
        return TestStepResult.Pass();
    }

    /// <summary>
    /// Проверяет перемычку термостата из регистра 1071 (без записи в результаты).
    /// </summary>
    private async Task<TestStepResult> CheckThermostatJumperAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Проверка перемычки термостата из регистра {Register}", RegisterThermostatJumper);

        var address = (ushort)(RegisterThermostatJumper - _settings.BaseAddressOffset);
        var result = await context.DiagReader.ReadUInt16Async(address, ct);

        if (!result.Success)
        {
            var msg = $"Ошибка при чтении перемычки термостата из регистра {RegisterThermostatJumper}. {result.Error}";
            logger.LogError(msg);
            return TestStepResult.Fail(msg);
        }

        var actualValue = result.Value;
        var isError = actualValue == 0;

        logger.LogInformation("Перемычка термостата: {Value} ({Status})", actualValue, isError ? "Open" : "Closed");

        if (isError)
        {
            const string msg = "Не установлена перемычка термостата (значение: 0)";
            logger.LogError(msg);
            return TestStepResult.Fail(msg, errors: [ErrorDefinitions.ThermostatJumperMissing]);
        }

        return TestStepResult.Pass();
    }
}

using Final_Test_Hybrid.Models.Errors;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Final_Test_Hybrid.Services.Results;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Limits;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Services.Steps.Steps.Coms;

/// <summary>
/// Тестовый шаг чтения и верификации версии ПО ЭБУ котла.
/// Читает major/minor версию из регистров 1055/1056 и проверяет соответствие диапазону из рецептов.
/// </summary>
public class ReadEcuVersionStep(
    IOptions<DiagnosticSettings> settings,
    ITestResultsService testResultsService,
    DualLogger<ReadEcuVersionStep> logger) : ITestStep, IRequiresRecipes, IProvideLimits
{
    private const ushort RegisterMajorVersion = 1055;
    private const ushort RegisterMinorVersion = 1056;

    private const string VersionMinRecipe = "Version_Min";
    private const string VersionMaxRecipe = "Version_Max";
    private const string VersionResultName = "Version";

    private readonly DiagnosticSettings _settings = settings.Value;

    public string Id => "coms-read-ecu-version";
    public string Name => "Coms/Read_ECU_Version";
    public string Description => "Чтение версии прошивки котла";

    public IReadOnlyList<string> RequiredRecipeAddresses => [VersionMinRecipe, VersionMaxRecipe];
    
    /// <summary>
    /// Возвращает пределы для отображения в гриде.
    /// </summary>
    public string? GetLimits(LimitsContext context)
    {
        var versionMinRecipe = context.RecipeProvider.GetValue<float>(VersionMinRecipe);
        var versionMaxRecipe = context.RecipeProvider.GetValue<float>(VersionMaxRecipe);
        return versionMinRecipe != null && versionMaxRecipe != null ? $"{versionMinRecipe} .. {versionMaxRecipe}" : null;
    }

    /// <summary>
    /// Выполняет чтение и верификацию версии ПО ЭБУ.
    /// </summary>
    /// <param name="context">Контекст выполнения шага.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        ClearPreviousResults();

        var versionMinStr = context.RecipeProvider.GetStringValue(VersionMinRecipe) ?? "0.0";
        var versionMaxStr = context.RecipeProvider.GetStringValue(VersionMaxRecipe) ?? "999.999";

        logger.LogInformation("Чтение версии ПО ЭБУ из регистров {Major} и {Minor}", RegisterMajorVersion, RegisterMinorVersion);

        var majorResult = await ReadMajorVersionAsync(context, ct);
        if (!majorResult.Success)
        {
            return majorResult.Result!;
        }

        var minorResult = await ReadMinorVersionAsync(context, ct);
        if (!minorResult.Success)
        {
            return minorResult.Result!;
        }

        var actualVersion = $"{majorResult.Value}.{minorResult.Value}";
        var actualNumeric = majorResult.Value * 1000 + minorResult.Value;

        var minNumeric = ParseVersionToNumeric(versionMinStr);
        var maxNumeric = ParseVersionToNumeric(versionMaxStr);
        var isInRange = actualNumeric >= minNumeric && actualNumeric <= maxNumeric;

        testResultsService.Add(
            parameterName: VersionResultName,
            value: actualVersion,
            min: versionMinStr,
            max: versionMaxStr,
            status: isInRange ? 1 : 2,
            isRanged: true,
            unit: "",
            test: Name);

        logger.LogInformation("Версия ПО ЭБУ: {Actual}, диапазон: [{Min}..{Max}], статус: {Status}",
            actualVersion, versionMinStr, versionMaxStr, isInRange ? "OK" : "NOK");

        var msg = $"Версия ПО ЭБУ: {actualVersion} [{versionMinStr}..{versionMaxStr}]";

        if (!isInRange)
        {
            logger.LogError("Версия ПО ЭБУ ({Actual}) вне допустимого диапазона [{Min}..{Max}]",
                actualVersion, versionMinStr, versionMaxStr);
            return TestStepResult.Fail(msg, errors: [ErrorDefinitions.EcuFirmwareVersionMismatch]);
        }

        return TestStepResult.Pass(msg);
    }

    /// <summary>
    /// Читает major-версию из регистра 1055.
    /// </summary>
    private async Task<(bool Success, ushort Value, TestStepResult? Result)> ReadMajorVersionAsync(
        TestStepContext context, CancellationToken ct)
    {
        var address = (ushort)(RegisterMajorVersion - _settings.BaseAddressOffset);
        var result = await context.DiagReader.ReadUInt16Async(address, ct);

        if (!result.Success)
        {
            var msg = $"Ошибка при чтении major-версии из регистра {RegisterMajorVersion}. {result.Error}";
            logger.LogError(msg);
            return (false, 0, TestStepResult.Fail(msg));
        }

        return (true, result.Value, null);
    }

    /// <summary>
    /// Читает minor-версию из регистра 1056.
    /// </summary>
    private async Task<(bool Success, ushort Value, TestStepResult? Result)> ReadMinorVersionAsync(
        TestStepContext context, CancellationToken ct)
    {
        var address = (ushort)(RegisterMinorVersion - _settings.BaseAddressOffset);
        var result = await context.DiagReader.ReadUInt16Async(address, ct);

        if (!result.Success)
        {
            var msg = $"Ошибка при чтении minor-версии из регистра {RegisterMinorVersion}. {result.Error}";
            logger.LogError(msg);
            return (false, 0, TestStepResult.Fail(msg));
        }

        return (true, result.Value, null);
    }

    /// <summary>
    /// Преобразует строку версии "110.5" в числовое значение 110005 для сравнения.
    /// </summary>
    private static int ParseVersionToNumeric(string versionStr)
    {
        var parts = versionStr.Split('.');
        var major = int.Parse(parts[0]);
        var minor = parts.Length > 1 ? int.Parse(parts[1]) : 0;
        return major * 1000 + minor;
    }

    /// <summary>
    /// Очищает предыдущие результаты для поддержки Retry.
    /// </summary>
    private void ClearPreviousResults()
    {
        testResultsService.Remove(VersionResultName);
    }
}

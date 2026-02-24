using System.Globalization;
using Final_Test_Hybrid.Models;
using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Models.Plc.Tags;
using Final_Test_Hybrid.Models.Plc.Subcription;
using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Main.Messages;
using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.Scanner;
using Final_Test_Hybrid.Services.SpringBoot.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Validation;

namespace Final_Test_Hybrid.Services.Steps.Steps;

/// <summary>
/// Базовый класс для шагов сканирования (Non-MES и MES).
/// Содержит общую логику подготовки к тесту.
/// </summary>
public abstract class ScanStepBase(
    BarcodeScanService barcodeScanService,
    ITestSequenceLoader sequenceLoader,
    ITestMapBuilder mapBuilder,
    ITestMapResolver mapResolver,
    RecipeValidator recipeValidator,
    BoilerState boilerState,
    PausableOpcUaTagService opcUa,
    IRecipeProvider recipeProvider,
    ExecutionPhaseState phaseState)
    : IPreExecutionStep, IRequiresPlcTags
{
    protected readonly BarcodeScanService BarcodeScanService = barcodeScanService;
    protected readonly ITestSequenceLoader SequenceLoader = sequenceLoader;
    protected readonly ITestMapBuilder MapBuilder = mapBuilder;
    protected readonly ITestMapResolver MapResolver = mapResolver;
    protected readonly RecipeValidator RecipeValidator = recipeValidator;
    protected readonly BoilerState BoilerState = boilerState;
    protected readonly PausableOpcUaTagService OpcUa = opcUa;
    protected readonly IRecipeProvider RecipeProvider = recipeProvider;
    protected readonly ExecutionPhaseState PhaseState = phaseState;

    private const string AppVersionRecipe = "App_Version";
    private const string PlantIdRecipe = "Plant_ID";
    private const string UnknownOperator = "Unknown";

    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    public bool IsVisibleInStatusGrid => true;
    public bool IsSkippable => true;
    public IReadOnlyList<string> RequiredPlcTags => [SensorScreenTags.GasPa, SensorScreenTags.GasP];

    public abstract Task<PreExecutionResult> ExecuteAsync(
        PreExecutionContext context,
        CancellationToken ct);

    protected abstract IDualLogger Logger { get; }

    #region Валидация баркода

    protected PreExecutionResult? ValidateBarcode(PreExecutionContext context)
    {
        var validation = BarcodeScanService.Validate(context.Barcode);
        if (!validation.IsValid)
        {
            return PreExecutionResult.Fail(validation.Error!);
        }
        context.BarcodeValidation = validation;
        Logger.LogInformation("Штрихкод валиден: {Barcode}, артикул: {Article}",
            validation.Barcode, validation.Article);
        return null;
    }

    #endregion

    #region Загрузка и построение maps

    protected async Task<PreExecutionResult?> LoadTestSequenceAsync(PreExecutionContext context)
    {
        var article = GetArticle(context);
        var result = await SequenceLoader.LoadRawDataAsync(article);
        if (!result.IsSuccess)
        {
            return PreExecutionResult.Fail(result.Error!);
        }
        context.RawSequenceData = result.RawData;
        Logger.LogInformation("Последовательность загружена: {Rows} строк", result.RawData!.Count);
        return null;
    }

    protected PreExecutionResult? BuildTestMaps(PreExecutionContext context)
    {
        var result = MapBuilder.Build(context.RawSequenceData!);
        if (!result.IsSuccess)
        {
            return PreExecutionResult.Fail(result.Error!);
        }
        context.RawMaps = result.Maps;
        Logger.LogInformation("Построено карт тестов: {Count}", result.Maps!.Count);
        return null;
    }

    protected PreExecutionResult? ResolveTestMaps(PreExecutionContext context)
    {
        PhaseState.SetPhase(ExecutionPhase.ValidatingSteps);
        if (context.RawMaps is not { Count: > 0 })
        {
            return PreExecutionResult.Fail("Нет тестовых последовательностей", "Ошибка проверки шагов");
        }
        var resolveResult = MapResolver.Resolve(context.RawMaps);
        if (resolveResult.UnknownSteps.Count > 0)
        {
            var error = $"Неизвестных шагов: {resolveResult.UnknownSteps.Count}";
            Logger.LogWarning("{Error}", error);
            return PreExecutionResult.Fail(error, new UnknownStepsDetails(resolveResult.UnknownSteps), "Ошибка проверки шагов");
        }
        context.Maps = resolveResult.Maps;
        Logger.LogInformation("Проверка шагов завершена: {Count} maps", context.Maps!.Count);
        return null;
    }

    private static string GetArticle(PreExecutionContext context)
    {
        return context.BoilerTypeCycle?.Article ?? context.BarcodeValidation!.Article!;
    }

    #endregion

    #region Валидация рецептов

    protected PreExecutionResult? ValidateRecipes(PreExecutionContext context)
    {
        PhaseState.SetPhase(ExecutionPhase.ValidatingRecipes);
        if (context.Maps == null || context.Maps.Count == 0)
        {
            return null;
        }
        var allSteps = ExtractAllSteps(context.Maps);
        var validation = RecipeValidator.Validate(allSteps, BoilerState.Recipes);
        if (!validation.IsValid)
        {
            var error = $"Отсутствуют рецепты: {validation.MissingRecipes.Count}";
            Logger.LogWarning("{Error}", error);
            return PreExecutionResult.Fail(
                error,
                new MissingRecipesDetails(validation.MissingRecipes),
                "Ошибка проверки рецептов");
        }
        Logger.LogInformation("Проверка рецептов пройдена");
        return null;
    }

    private static List<ITestStep> ExtractAllSteps(List<TestMap> maps)
    {
        return maps
            .SelectMany(m => m.Rows)
            .SelectMany(r => r.Steps)
            .Where(s => s != null)
            .Cast<ITestStep>()
            .Distinct()
            .ToList();
    }

    #endregion

    #region Сохранение состояния

    protected void SaveBoilerState(PreExecutionContext context)
    {
        var validation = context.BarcodeValidation!;
        BoilerState.SetData(
            validation.Barcode,
            validation.Article!,
            isValid: true,
            context.BoilerTypeCycle,
            context.Recipes);
        Logger.LogInformation("Состояние котла сохранено: {Barcode}, {Article}",
            validation.Barcode, validation.Article);
    }

    #endregion

    #region Запись рецептов в PLC

    /// <summary>
    /// Размер батча для записи рецептов. 50 — консервативное значение,
    /// поддерживаемое большинством OPC-UA серверов (типичный лимит 1000+).
    /// </summary>
    private const int BatchSize = 50;

    /// <summary>
    /// Записывает рецепты в PLC батчами.
    /// </summary>
    protected async Task<PreExecutionResult?> WriteRecipesToPlcAsync(PreExecutionContext context, CancellationToken ct)
    {
        var recipes = GetPlcRecipes();
        if (recipes.Count == 0)
        {
            return null;
        }
        return await WriteAllRecipesAsync(recipes, context, ct);
    }

    /// <summary>
    /// Возвращает список рецептов для записи в PLC.
    /// </summary>
    private List<RecipeResponseDto> GetPlcRecipes()
    {
        return BoilerState.Recipes?
            .Where(r => r.IsPlc)
            .ToList() ?? [];
    }

    /// <summary>
    /// Записывает все рецепты в PLC с использованием батчинга.
    /// </summary>
    private async Task<PreExecutionResult?> WriteAllRecipesAsync(
        List<RecipeResponseDto> recipes,
        PreExecutionContext context,
        CancellationToken ct)
    {
        PhaseState.SetPhase(ExecutionPhase.LoadingRecipes);

        // 1. Парсинг всех значений (fail-fast при ошибке парсинга)
        var items = new List<(string nodeId, object value, RecipeResponseDto recipe)>();
        foreach (var recipe in recipes)
        {
            var parseResult = TryParseRecipeValue(recipe);
            if (!parseResult.IsSuccess)
            {
                return CreateParseError(recipe, parseResult.Error!);
            }
            items.Add((recipe.Address, parseResult.Value!, recipe));
        }

        // 2. Chunked batch write (по BatchSize рецептов за запрос)
        var chunks = items.Chunk(BatchSize);
        foreach (var chunk in chunks)
        {
            ct.ThrowIfCancellationRequested();

            var batchItems = chunk.Select(i => (i.nodeId, i.value)).ToList();
            LogBatchWrite(chunk);

            var results = await context.OpcUa.WriteBatchAsync(batchItems, ct);

            // Fail-fast: при первой ошибке — прервать
            var error = FindFirstError(chunk, results);
            if (error != null)
            {
                return error;
            }

            LogBatchSuccess(chunk);
        }

        return null;
    }

    /// <summary>
    /// Логирует начало записи батча.
    /// </summary>
    private void LogBatchWrite(IEnumerable<(string nodeId, object value, RecipeResponseDto recipe)> items)
    {
        foreach (var (_, _, recipe) in items)
        {
            Logger.LogInformation("WriteRecipe: Tag={Tag}, Value={Value}, Type={Type}",
                recipe.TagName, recipe.Value, recipe.PlcType);
        }
    }

    /// <summary>
    /// Логирует успешную запись батча.
    /// </summary>
    private void LogBatchSuccess(IEnumerable<(string nodeId, object value, RecipeResponseDto recipe)> items)
    {
        foreach (var (_, _, recipe) in items)
        {
            Logger.LogInformation("Записан рецепт {Tag} = {Value}", recipe.TagName, recipe.Value);
        }
    }

    /// <summary>
    /// Находит первую ошибку в результатах записи.
    /// </summary>
    private PreExecutionResult? FindFirstError(
        IEnumerable<(string nodeId, object value, RecipeResponseDto recipe)> items,
        List<WriteResult> results)
    {
        var itemsList = items.ToList();
        for (var i = 0; i < results.Count; i++)
        {
            if (!results[i].Success)
            {
                var recipe = itemsList[i].recipe;
                var message = $"Ошибка записи {recipe.TagName}: {results[i].Error}";
                Logger.LogError("{Error}", message);
                var errorInfo = new RecipeWriteErrorInfo(recipe.TagName, recipe.Address, recipe.Value, results[i].Error!);
                return PreExecutionResult.Fail(message, new RecipeWriteErrorDetails([errorInfo]), "Ошибка загрузки рецептов");
            }
        }
        return null;
    }

    /// <summary>
    /// Парсит значение рецепта в соответствии с типом PLC.
    /// </summary>
    private ParseResult TryParseRecipeValue(RecipeResponseDto recipe)
    {
        return recipe.PlcType switch
        {
            PlcType.REAL => TryParseFloat(recipe.Value, out var f)
                ? ParseResult.Ok(f)
                : ParseResult.Fail($"Некорректное значение '{recipe.Value}' для типа float"),
            PlcType.INT16 => short.TryParse(recipe.Value, CultureInfo.InvariantCulture, out var s)
                ? ParseResult.Ok(s)
                : ParseResult.Fail($"Некорректное значение '{recipe.Value}' для типа Int16"),
            PlcType.DINT => int.TryParse(recipe.Value, CultureInfo.InvariantCulture, out var d)
                ? ParseResult.Ok(d)
                : ParseResult.Fail($"Некорректное значение '{recipe.Value}' для типа Dint"),
            PlcType.BOOL => ParseResult.Ok(ParseBool(recipe.Value)),
            PlcType.STRING => ParseResult.Ok(recipe.Value),
            _ => ParseResult.Fail($"Неизвестный тип: {recipe.PlcType}")
        };
    }

    /// <summary>
    /// Результат парсинга значения рецепта.
    /// </summary>
    private record ParseResult(bool IsSuccess, object? Value, string? Error)
    {
        public static ParseResult Ok(object value) => new(true, value, null);
        public static ParseResult Fail(string error) => new(false, null, error);
    }

    /// <summary>
    /// Создаёт ошибку парсинга рецепта.
    /// </summary>
    private PreExecutionResult CreateParseError(RecipeResponseDto recipe, string error)
    {
        Logger.LogError("{Error}", error);
        var errorInfo = new RecipeWriteErrorInfo(recipe.TagName, recipe.Address, recipe.Value, error);
        return PreExecutionResult.Fail(error, new RecipeWriteErrorDetails([errorInfo]), "Ошибка загрузки рецептов");
    }

    /// <summary>
    /// Парсит строковое значение в float.
    /// </summary>
    private static bool TryParseFloat(string value, out float result)
    {
        return float.TryParse(value.Replace(',', '.'), CultureInfo.InvariantCulture, out result);
    }

    /// <summary>
    /// Парсит строковое значение в bool.
    /// </summary>
    private static bool ParseBool(string value)
    {
        return value.Equals("true", StringComparison.OrdinalIgnoreCase)
               || value.Equals("1", StringComparison.Ordinal);
    }

    #endregion

    #region Инициализация провайдера рецептов

    protected void InitializeRecipeProvider()
    {
        var recipes = BoilerState.Recipes ?? [];
        RecipeProvider.SetRecipes(recipes);
        Logger.LogInformation("Рецепты загружены в провайдер: {Count}", recipes.Count);
    }

    protected void CaptureScanServiceContext(PreExecutionContext context, string testerNumber, int? shiftNumber)
    {
        var appVersion = RecipeProvider.GetStringValue(AppVersionRecipe) ?? string.Empty;
        var plantId = RecipeProvider.GetStringValue(PlantIdRecipe) ?? string.Empty;
        var shiftNo = shiftNumber?.ToString(CultureInfo.InvariantCulture) ?? "0";
        var testerNo = string.IsNullOrWhiteSpace(testerNumber) ? UnknownOperator : testerNumber;

        context.ScanServiceContext = new ScanServiceContext
        {
            AppVersion = appVersion,
            PlantId = plantId,
            ShiftNo = shiftNo,
            TesterNo = testerNo
        };

        Logger.LogInformation(
            "Собран scan-контекст: App_Version={AppVersion}, Plant_ID={PlantId}, Shift_No={ShiftNo}, Tester_No={TesterNo}",
            appVersion,
            plantId,
            shiftNo,
            testerNo);
    }

    public async Task<(bool Success, float GasPa, float GasP, PreExecutionResult? Error)> ReadPressuresAsync(CancellationToken ct)
    {
        var gasPa = await TryReadPressureAsync(SensorScreenTags.GasPa, "Gas_Pa", ct);
        if (!gasPa.Success)
        {
            return (false, 0, 0, gasPa.Error);
        }

        var gasP = await TryReadPressureAsync(SensorScreenTags.GasP, "Gas_P", ct);
        if (!gasP.Success)
        {
            return (false, 0, 0, gasP.Error);
        }

        Logger.LogInformation("Считаны давления ScanBarcode: Pres_atmosph.={GasPa}, Pres_in_gas={GasP}", gasPa.Value, gasP.Value);
        return (true, gasPa.Value, gasP.Value, null);
    }

    private async Task<(bool Success, float Value, PreExecutionResult? Error)> TryReadPressureAsync(
        string nodeId,
        string tagName,
        CancellationToken ct)
    {
        var (_, value, error) = await OpcUa.ReadAsync<float>(nodeId, ct);
        if (error == null)
        {
            return (true, value, null);
        }

        return (false, 0, PreExecutionResult.Fail($"Ошибка чтения {tagName}: {error}"));
    }

    #endregion
}

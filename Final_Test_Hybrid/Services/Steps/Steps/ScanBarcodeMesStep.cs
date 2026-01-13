using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.Scanner;
using Final_Test_Hybrid.Services.SpringBoot.Operation;
using Final_Test_Hybrid.Services.SpringBoot.Operator;
using Final_Test_Hybrid.Services.SpringBoot.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Validation;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Steps;

/// <summary>
/// Шаг сканирования для MES режима.
/// Выполняет полную подготовку к тесту: валидация, запрос в MES, построение maps, запись в PLC.
/// </summary>
public class ScanBarcodeMesStep(
    BarcodeScanService barcodeScanService,
    ITestSequenceLoader sequenceLoader,
    ITestMapBuilder mapBuilder,
    ITestMapResolver mapResolver,
    RecipeValidator recipeValidator,
    BoilerState boilerState,
    PausableOpcUaTagService opcUa,
    IRecipeProvider recipeProvider,
    ExecutionMessageState messageState,
    OperationStartService operationStartService,
    OperatorState operatorState,
    OrderState orderState,
    ILogger<ScanBarcodeMesStep> logger,
    ITestStepLogger testStepLogger)
    : ScanStepBase(barcodeScanService, sequenceLoader, mapBuilder, mapResolver,
        recipeValidator, boilerState, opcUa, recipeProvider, messageState)
{
    private readonly DualLogger<ScanBarcodeMesStep> _logger = new(logger, testStepLogger);

    private const string UnknownOperator = "Unknown";

    /// <summary>
    /// Callback для обработки rework flow (показ диалога в UI).
    /// </summary>
    public Func<string, Func<string, string, Task<ReworkSubmitResult>>, Task<ReworkFlowResult>>? OnReworkRequired { get; set; }

    public override string Id => "scan-barcode-mes";
    public override string Name => "Сканирование штрихкода MES";
    public override string Description => "Сканирует штрихкод и получает данные из MES";
    protected override IDualLogger Logger => _logger;

    private string CurrentOperatorName => operatorState.Username ?? UnknownOperator;

    public override async Task<PreExecutionResult> ExecuteAsync(PreExecutionContext context, CancellationToken ct)
    {
        _logger.LogStepStart(Name);
        MessageState.SetMessage("Штрихкод получен");

        // 1. Валидация баркода
        var validateError = ValidateBarcode(context);
        if (validateError != null) return validateError;

        // 2. Запрос в MES (получаем BoilerTypeCycle + Recipes)
        var mesError = await StartOperationMesAsync(context, ct);
        if (mesError != null) return mesError;

        // 3. Загрузка последовательности тестов
        var loadSequenceError = await LoadTestSequenceAsync(context);
        if (loadSequenceError != null) return loadSequenceError;

        // 4. Построение карт тестов
        var buildMapsError = BuildTestMaps(context);
        if (buildMapsError != null) return buildMapsError;

        // 5. Сохранение состояния котла
        SaveBoilerState(context);

        // 6. Резолв карт тестов
        var resolveMapsError = ResolveTestMaps(context);
        if (resolveMapsError != null) return resolveMapsError;

        // 7. Валидация рецептов
        var validateRecipesError = ValidateRecipes(context);
        if (validateRecipesError != null) return validateRecipesError;

        // 8. Пропускаем инициализацию БД (в MES режиме не нужна)

        // 9. Запись рецептов в PLC
        var writePlcError = await WriteRecipesToPlcAsync(context, ct);
        if (writePlcError != null) return writePlcError;

        // 10. Инициализация провайдера рецептов
        InitializeRecipeProvider();

        _logger.LogStepEnd(Name);
        return PreExecutionResult.Continue(context.Barcode);
    }

    #region MES специфичная логика

    private async Task<PreExecutionResult?> StartOperationMesAsync(PreExecutionContext context, CancellationToken ct)
    {
        var barcode = context.BarcodeValidation!.Barcode;
        var result = await operationStartService.StartOperationAsync(barcode, CurrentOperatorName, ct: ct);
        if (result.IsSuccess)
        {
            return HandleSuccessfulStart(context, result.Data!);
        }
        return await HandleOperationFailure(context, result);
    }

    private PreExecutionResult? HandleSuccessfulStart(PreExecutionContext context, OperationStartResponse data)
    {
        if (data.Recipes.Count == 0)
        {
            return PreExecutionResult.Fail("Рецепты не найдены");
        }
        context.BoilerTypeCycle = MapToBoilerTypeCycle(data.BoilerTypeCycle);
        context.Recipes = MapRecipes(data.Recipes);
        SaveOrderState(data.BoilerMadeInformation);
        _logger.LogInformation("MES: Тип: {Type}, Артикул: {Article}, Рецептов: {Count}",
            data.BoilerTypeCycle.TypeName, data.BoilerTypeCycle.Article, data.Recipes.Count);
        return null;
    }

    private async Task<PreExecutionResult?> HandleOperationFailure(PreExecutionContext context, OperationStartResult result)
    {
        if (result.RequiresRework)
        {
            return await HandleReworkFlowAsync(context, result.ErrorMessage!);
        }
        return PreExecutionResult.Fail(result.ErrorMessage!);
    }

    private async Task<PreExecutionResult?> HandleReworkFlowAsync(PreExecutionContext context, string errorMessage)
    {
        _logger.LogWarning("Требуется доработка: {ErrorMessage}", errorMessage);
        if (OnReworkRequired == null)
        {
            return PreExecutionResult.Fail("Обработчик доработки не настроен");
        }
        var flowResult = await OnReworkRequired(
            errorMessage,
            (adminUsername, reason) => ExecuteReworkRequestAsync(context, adminUsername, reason));
        return flowResult.IsCancelled ? PreExecutionResult.Cancelled(flowResult.ErrorMessage ?? errorMessage) : null;
    }

    private async Task<ReworkSubmitResult> ExecuteReworkRequestAsync(
        PreExecutionContext context,
        string adminUsername,
        string reason)
    {
        _logger.LogInformation("Запрос на доработку: Admin={Admin}, Reason={Reason}", adminUsername, reason);
        var barcode = context.BarcodeValidation!.Barcode;
        var reworkResult = await operationStartService.ReworkAsync(barcode, CurrentOperatorName, adminUsername, reason);
        if (!reworkResult.IsSuccess)
        {
            return ReworkSubmitResult.Fail(reworkResult.ErrorMessage!);
        }
        _logger.LogInformation("Доработка одобрена, повторный запрос в MES...");
        return await RetryOperationStartAsync(context);
    }

    private async Task<ReworkSubmitResult> RetryOperationStartAsync(PreExecutionContext context)
    {
        var barcode = context.BarcodeValidation!.Barcode;
        var result = await operationStartService.StartOperationAsync(barcode, CurrentOperatorName);
        if (!result.IsSuccess)
        {
            return ReworkSubmitResult.Fail(result.ErrorMessage!);
        }
        if (result.Data == null)
        {
            return ReworkSubmitResult.Fail("Сервер вернул пустой ответ");
        }
        HandleSuccessfulStart(context, result.Data);
        return ReworkSubmitResult.Success();
    }

    private void SaveOrderState(BoilerMadeInformation info)
    {
        orderState.SetData(info.OrderNumber, info.AmountBoilerOrder, info.AmountBoilerMadeOrder);
    }

    private static BoilerTypeCycle MapToBoilerTypeCycle(BoilerTypeCycleDto dto)
    {
        return new BoilerTypeCycle
        {
            Type = dto.TypeName,
            Article = dto.Article
        };
    }

    private static IReadOnlyList<RecipeResponseDto> MapRecipes(List<RecipeDto> recipes)
    {
        return recipes.Select(r => new RecipeResponseDto
        {
            TagName = r.Parameter,
            Address = r.Parameter,
            Value = r.Value,
            PlcType = MapPlcType(r.PlcType)
        }).ToList();
    }

    private static PlcType MapPlcType(PlcTypeDto plcType)
    {
        return plcType switch
        {
            PlcTypeDto.STRING => PlcType.STRING,
            PlcTypeDto.INT => PlcType.INT16,
            PlcTypeDto.INT16 => PlcType.INT16,
            PlcTypeDto.DINT => PlcType.DINT,
            PlcTypeDto.REAL => PlcType.REAL,
            PlcTypeDto.BOOL => PlcType.BOOL,
            _ => PlcType.STRING
        };
    }

    #endregion
}

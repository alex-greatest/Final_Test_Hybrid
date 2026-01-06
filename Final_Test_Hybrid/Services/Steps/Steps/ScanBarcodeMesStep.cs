using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Scanner;
using Final_Test_Hybrid.Services.SpringBoot.Operation;
using Final_Test_Hybrid.Services.SpringBoot.Operator;
using Final_Test_Hybrid.Services.SpringBoot.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Scanning;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.PreExecution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Steps;

public class ScanBarcodeMesStep(
    BarcodeScanService barcodeScanService,
    OperationStartService operationStartService,
    OperatorState operatorState,
    BoilerState boilerState,
    OrderState orderState,
    ITestSequenceLoader sequenceLoader,
    ITestMapBuilder mapBuilder,
    ExecutionMessageState messageState,
    ILogger<ScanBarcodeMesStep> logger,
    ITestStepLogger testStepLogger) : ITestStep, IScanBarcodeStep, IPreExecutionStep
{
    public Func<string, Func<string, string, Task<ReworkSubmitResult>>, Task<ReworkFlowResult>>? OnReworkRequired { get; set; }
    private const string UnknownOperator = "Unknown";
    private string CurrentOperatorName => operatorState.Username ?? UnknownOperator;

    public string Id => "scan-barcode-mes";
    public string Name => "Сканирование штрихкода MES";
    public string Description => "Сканирует штрихкод и отправляет в MES";
    public bool IsVisibleInEditor => false;
    public bool IsVisibleInStatusGrid => true;

    public Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        return Task.FromResult(TestStepResult.Pass());
    }

    public async Task<BarcodeStepResult> ProcessBarcodeAsync(string barcode)
    {
        testStepLogger.LogStepStart(Name);
        LogInfo("Обработка штрихкода MES: {Barcode}", barcode);
        var pipeline = new BarcodePipeline(barcode);
        var result = await ExecutePipelineAsync(pipeline);
        return !result.IsSuccess ? result : CompleteSuccessfully(pipeline);
    }

    private async Task<BarcodeStepResult> ExecutePipelineAsync(BarcodePipeline pipeline)
    {
        return await pipeline
            .Step("Проверка штрихкода...", ValidateBarcode)
            .StepAsync("Запрос в MES...", StartOperationAsync)
            .StepAsync("Загрузка последовательности...", LoadTestSequenceAsync)
            .Step("Построение карт тестов...", BuildTestMaps)
            .ExecuteAsync(messageState);
    }

    private BarcodeStepResult? ValidateBarcode(BarcodePipeline pipeline)
    {
        pipeline.Validation = barcodeScanService.Validate(pipeline.Barcode);
        return !pipeline.Validation.IsValid ? Fail(pipeline.Validation.Error!) : null;
    }

    private async Task<BarcodeStepResult?> StartOperationAsync(BarcodePipeline pipeline)
    {
        var result = await operationStartService.StartOperationAsync(
            pipeline.Validation.Barcode,
            CurrentOperatorName);
        if (result.IsSuccess)
        {
            return result.Data is null ? Fail("Сервер вернул пустой ответ") : HandleSuccessfulStart(pipeline, result.Data);
        }
        return await HandleOperationFailure(pipeline, result);
    }

    private async Task<BarcodeStepResult?> HandleOperationFailure(BarcodePipeline pipeline, OperationStartResult result)
    {
        if (result.RequiresRework)
        {
            return await HandleReworkFlowAsync(pipeline, result.ErrorMessage!);
        }
        return Fail(result.ErrorMessage!);
    }

    private BarcodeStepResult? HandleSuccessfulStart(BarcodePipeline pipeline, OperationStartResponse data)
    {
        pipeline.Recipes = MapRecipes(data.Recipes);
        if (pipeline.Recipes.Count == 0)
        {
            return Fail("Рецепты не найдены");
        }
        pipeline.Cycle = MapToBoilerTypeCycle(data.BoilerTypeCycle);
        SaveOrderState(data.BoilerMadeInformation);
        LogLoadedRecipes(pipeline.Recipes);
        LogInfo("MES: Тип: {Type}, Артикул: {Article}, Рецептов: {Count}",
            data.BoilerTypeCycle.TypeName,
            data.BoilerTypeCycle.Article,
            data.Recipes.Count);
        return null;
    }

    private void LogLoadedRecipes(IReadOnlyList<RecipeResponseDto> recipes)
    {
        LogInfo("Загружено рецептов: {Count}", recipes.Count);
        foreach (var recipe in recipes)
        {
            LogInfo("Рецепт: {TagName} = {Value} ({PlcType})", recipe.TagName, recipe.Value, recipe.PlcType);
        }
    }

    private void SaveOrderState(BoilerMadeInformation info)
    {
        orderState.SetData(info.OrderNumber, info.AmountBoilerOrder, info.AmountBoilerMadeOrder);
    }

    private async Task<BarcodeStepResult?> HandleReworkFlowAsync(BarcodePipeline pipeline, string errorMessage)
    {
        LogWarning("Требуется доработка: {ErrorMessage}", errorMessage);
        if (OnReworkRequired == null)
        {
            return Fail("Обработчик доработки не настроен");
        }
        var flowResult = await OnReworkRequired(
            errorMessage,
            (adminUsername, reason) => ExecuteReworkRequestAsync(pipeline, adminUsername, reason));
        return flowResult.IsCancelled ? BarcodeStepResult.Cancelled(flowResult.ErrorMessage ?? errorMessage) : null;
    }

    private async Task<ReworkSubmitResult> ExecuteReworkRequestAsync(
        BarcodePipeline pipeline,
        string adminUsername,
        string reason)
    {
        LogInfo("Запрос на доработку: Admin={Admin}, Reason={Reason}", adminUsername, reason);
        var reworkResult = await RequestReworkApprovalAsync(pipeline, adminUsername);
        if (!reworkResult.IsSuccess) { return reworkResult; }
        return await RetryOperationStartAsync(pipeline);
    }

    private async Task<ReworkSubmitResult> RequestReworkApprovalAsync(BarcodePipeline pipeline, string adminUsername)
    {
        var result = await operationStartService.ReworkAsync(
            pipeline.Validation.Barcode,
            CurrentOperatorName,
            adminUsername);
        if (!result.IsSuccess) { return ReworkSubmitResult.Fail(result.ErrorMessage!); }
        LogInfo("Доработка одобрена, повторный запрос в MES...");
        return ReworkSubmitResult.Success();
    }

    private async Task<ReworkSubmitResult> RetryOperationStartAsync(BarcodePipeline pipeline)
    {
        var result = await operationStartService.StartOperationAsync(
            pipeline.Validation.Barcode,
            CurrentOperatorName);
        var error = GetOperationError(result);
        if (error != null)
        {
            return ReworkSubmitResult.Fail(error);
        }
        HandleSuccessfulStart(pipeline, result.Data!);
        return ReworkSubmitResult.Success();
    }

    private static string? GetOperationError(OperationStartResult result)
    {
        if (!result.IsSuccess)
        {
            return result.ErrorMessage!;
        }
        return result.Data is null ? "Сервер вернул пустой ответ" : null;
    }

    private async Task<BarcodeStepResult?> LoadTestSequenceAsync(BarcodePipeline pipeline)
    {
        var article = pipeline.Cycle.Article;
        var result = await sequenceLoader.LoadRawDataAsync(article);
        if (!result.IsSuccess)
        {
            return Fail(result.Error!);
        }
        pipeline.RawSequenceData = result.RawData!;
        return null;
    }

    private BarcodeStepResult? BuildTestMaps(BarcodePipeline pipeline)
    {
        var result = mapBuilder.Build(pipeline.RawSequenceData);
        if (!result.IsSuccess)
        {
            return Fail(result.Error!);
        }
        pipeline.RawMaps = result.Maps!;
        return null;
    }

    private BarcodeStepResult CompleteSuccessfully(BarcodePipeline pipeline)
    {
        LogSuccessfulProcessing(pipeline);
        SaveBoilerState(pipeline);
        testStepLogger.LogStepEnd(Name);
        return BarcodeStepResult.Pass(pipeline.RawMaps);
    }

    private void LogSuccessfulProcessing(BarcodePipeline pipeline)
    {
        LogInfo("Успешно: {Serial}, {Article}, {Type}, рецептов: {Count}, RawMaps: {Maps}",
            pipeline.Validation.Barcode,
            pipeline.Cycle.Article,
            pipeline.Cycle.Type,
            pipeline.Recipes.Count,
            pipeline.RawMaps.Count);
    }

    private void SaveBoilerState(BarcodePipeline pipeline)
    {
        boilerState.SetData(
            pipeline.Validation.Barcode,
            pipeline.Cycle.Article,
            isValid: true,
            pipeline.Cycle,
            pipeline.Recipes);
    }

    private static IReadOnlyList<RecipeResponseDto> MapRecipes(List<RecipeDto> recipes)
    {
        return recipes.Select(r => new RecipeResponseDto
        {
            TagName = r.Parameter,
            Value = r.Value,
            PlcType = MapPlcType(r.PlcType)
        }).ToList();
    }

    private static PlcType MapPlcType(PlcTypeDto plcType)
    {
        return plcType switch
        {
            PlcTypeDto.STRING => PlcType.String,
            PlcTypeDto.INT => PlcType.Int16,
            PlcTypeDto.INT16 => PlcType.Int16,
            PlcTypeDto.DINT => PlcType.Dint,
            PlcTypeDto.REAL => PlcType.Real,
            PlcTypeDto.BOOL => PlcType.Bool,
            _ => PlcType.String
        };
    }

    private static BoilerTypeCycle MapToBoilerTypeCycle(BoilerTypeCycleDto dto)
    {
        return new BoilerTypeCycle
        {
            Type = dto.TypeName,
            Article = dto.Article
        };
    }

    private BarcodeStepResult Fail(string error)
    {
        testStepLogger.LogError(null, "{Error}", error);
        return BarcodeStepResult.Fail(error);
    }

    private void LogInfo(string message, params object?[] args)
    {
        logger.LogInformation(message, args);
        testStepLogger.LogInformation(message, args);
    }

    private void LogWarning(string message, params object?[] args)
    {
        logger.LogWarning(message, args);
        testStepLogger.LogWarning(message, args);
    }

    async Task<PreExecutionResult> IPreExecutionStep.ExecuteAsync(PreExecutionContext context, CancellationToken ct)
    {
        var result = await ProcessBarcodeAsync(context.Barcode);
        return MapToPreExecutionResult(result, context);
    }

    private PreExecutionResult MapToPreExecutionResult(BarcodeStepResult result, PreExecutionContext context)
    {
        if (result.IsCancelled)
        {
            return PreExecutionResult.Cancelled(result.ErrorMessage);
        }
        return HandleBarcodeCompletion(result, context);
    }

    private PreExecutionResult HandleBarcodeCompletion(BarcodeStepResult result, PreExecutionContext context)
    {
        if (!result.IsSuccess)
        {
            return PreExecutionResult.Fail(result.ErrorMessage!);
        }
        context.RawMaps = result.RawMaps;
        return PreExecutionResult.Continue();
    }
}

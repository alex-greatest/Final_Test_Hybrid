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
    ReworkDialogService reworkDialogService,
    OperatorState operatorState,
    BoilerState boilerState,
    OrderState orderState,
    ITestSequenceLoader sequenceLoader,
    ITestMapBuilder mapBuilder,
    ExecutionMessageState messageState,
    ILogger<ScanBarcodeMesStep> logger,
    ITestStepLogger testStepLogger) : ITestStep, IScanBarcodeStep, IPreExecutionStep
{
    private const string UnknownOperator = "Unknown";

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
            operatorState.Username ?? UnknownOperator);

        if (result.IsSuccess)
        {
            if (result.Data is null)
            {
                return Fail("Сервер вернул пустой ответ", LogLevel.Warning);
            }
            return HandleSuccessfulStart(pipeline, result.Data);
        }
        if (result.RequiresRework)
        {
            return await HandleReworkFlowAsync(pipeline, result.ErrorMessage!);
        }
        return Fail(result.ErrorMessage!, LogLevel.Warning);
    }

    private BarcodeStepResult? HandleSuccessfulStart(BarcodePipeline pipeline, OperationStartResponse data)
    {
        pipeline.Recipes = MapRecipes(data.Recipes);
        pipeline.Cycle = MapToBoilerTypeCycle(data.BoilerTypeCycle);
        SaveOrderState(data.BoilerMadeInformation);
        LogInfo("MES: Тип: {Type}, Артикул: {Article}, Рецептов: {Count}",
            data.BoilerTypeCycle.TypeName,
            data.BoilerTypeCycle.Article,
            data.Recipes.Count);
        return null;
    }

    private void SaveOrderState(BoilerMadeInformation info)
    {
        orderState.SetData(info.OrderNumber, info.AmountBoilerOrder, info.AmountBoilerMadeOrder);
    }

    private async Task<BarcodeStepResult?> HandleReworkFlowAsync(BarcodePipeline pipeline, string errorMessage)
    {
        LogWarning($"Требуется доработка: {errorMessage}");
        var flowResult = await reworkDialogService.ExecuteReworkFlowAsync(errorMessage);
        if (flowResult.IsCancelled)
        {
            return Fail("Операция отменена пользователем");
        }
        return await ExecuteReworkAsync(pipeline, flowResult.AdminUsername);
    }

    private async Task<BarcodeStepResult?> ExecuteReworkAsync(BarcodePipeline pipeline, string adminUsername)
    {
        var reworkResult = await operationStartService.ReworkAsync(
            pipeline.Validation.Barcode,
            operatorState.Username ?? UnknownOperator,
            adminUsername);

        if (!reworkResult.IsSuccess)
        {
            return Fail(reworkResult.ErrorMessage!, LogLevel.Warning);
        }
        LogInfo("Доработка одобрена, повторный запрос в MES...");
        return await RetryStartOperationAsync(pipeline);
    }

    private async Task<BarcodeStepResult?> RetryStartOperationAsync(BarcodePipeline pipeline)
    {
        var result = await operationStartService.StartOperationAsync(
            pipeline.Validation.Barcode,
            operatorState.Username ?? UnknownOperator);

        if (!result.IsSuccess)
        {
            return Fail(result.ErrorMessage!, LogLevel.Warning);
        }
        if (result.Data is null)
        {
            return Fail("Сервер вернул пустой ответ", LogLevel.Warning);
        }
        return HandleSuccessfulStart(pipeline, result.Data);
    }

    private async Task<BarcodeStepResult?> LoadTestSequenceAsync(BarcodePipeline pipeline)
    {
        var article = pipeline.Cycle.Article;
        var result = await sequenceLoader.LoadRawDataAsync(article);
        if (!result.IsSuccess)
        {
            return Fail(result.Error!, LogLevel.Warning);
        }
        pipeline.RawSequenceData = result.RawData!;
        return null;
    }

    private BarcodeStepResult? BuildTestMaps(BarcodePipeline pipeline)
    {
        var result = mapBuilder.Build(pipeline.RawSequenceData);
        if (!result.IsSuccess)
        {
            return Fail(result.Error!, LogLevel.Error);
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
            Address = string.Empty,
            PlcType = MapPlcType(r.PlcType),
            IsPlc = true
        }).ToList();
    }

    private static PlcType MapPlcType(PlcTypeDto plcType)
    {
        return plcType switch
        {
            PlcTypeDto.STRING => PlcType.String,
            PlcTypeDto.INT => PlcType.Int16,
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

    private BarcodeStepResult Fail(string error, LogLevel level = LogLevel.None)
    {
        LogByLevel(error, level);
        return BarcodeStepResult.Fail(error);
    }

    private void LogByLevel(string message, LogLevel level)
    {
        switch (level)
        {
            case LogLevel.Warning:
                LogWarning("{Message}", message);
                break;
            case LogLevel.Error:
                LogError("{Message}", message);
                break;
        }
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

    private void LogError(string message, params object?[] args)
    {
        logger.LogError(message, args);
        testStepLogger.LogError(null, message, args);
    }

    public Task OnExecutionStartingAsync()
    {
        return Task.CompletedTask;
    }

    async Task<PreExecutionResult> IPreExecutionStep.ExecuteAsync(PreExecutionContext context, CancellationToken ct)
    {
        var result = await ProcessBarcodeAsync(context.Barcode);
        if (!result.IsSuccess)
        {
            return PreExecutionResult.Fail(result.ErrorMessage!, "Ошибка. Повторите сканирование");
        }
        context.RawMaps = result.RawMaps;
        return PreExecutionResult.Ok();
    }
}

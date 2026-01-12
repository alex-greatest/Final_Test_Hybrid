using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.SpringBoot.Operation;
using Final_Test_Hybrid.Services.SpringBoot.Operator;
using Final_Test_Hybrid.Services.SpringBoot.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;

namespace Final_Test_Hybrid.Services.Steps.Steps.PreExecution;

public class StartOperationMesStep(
    OperationStartService operationStartService,
    OperatorState operatorState,
    OrderState orderState,
    DualLogger<StartOperationMesStep> logger) : IPreExecutionStep
{
    public Func<string, Func<string, string, Task<ReworkSubmitResult>>, Task<ReworkFlowResult>>? OnReworkRequired { get; set; }

    private const string UnknownOperator = "Unknown";

    public string Id => "start-operation-mes";
    public string Name => "Запрос в MES";
    public string Description => "Получение данных из MES системы";
    public bool IsVisibleInStatusGrid => false;

    private string CurrentOperatorName => operatorState.Username ?? UnknownOperator;

    public async Task<PreExecutionResult> ExecuteAsync(PreExecutionContext context, CancellationToken ct)
    {
        var barcode = context.BarcodeValidation!.Barcode;
        var result = await operationStartService.StartOperationAsync(barcode, CurrentOperatorName, ct: ct);
        if (result.IsSuccess)
        {
            return HandleSuccessfulStart(context, result.Data!);
        }
        return await HandleOperationFailure(context, result);
    }

    private async Task<PreExecutionResult> HandleOperationFailure(PreExecutionContext context, OperationStartResult result)
    {
        if (result.RequiresRework)
        {
            return await HandleReworkFlowAsync(context, result.ErrorMessage!);
        }
        return PreExecutionResult.Fail(result.ErrorMessage!);
    }

    private PreExecutionResult HandleSuccessfulStart(PreExecutionContext context, OperationStartResponse data)
    {
        if (data.Recipes.Count == 0)
        {
            return PreExecutionResult.Fail("Рецепты не найдены");
        }
        context.BoilerTypeCycle = MapToBoilerTypeCycle(data.BoilerTypeCycle);
        context.Recipes = MapRecipes(data.Recipes);
        SaveOrderState(data.BoilerMadeInformation);
        logger.LogInformation("MES: Тип: {Type}, Артикул: {Article}, Рецептов: {Count}",
            data.BoilerTypeCycle.TypeName, data.BoilerTypeCycle.Article, data.Recipes.Count);
        return PreExecutionResult.Continue();
    }

    private async Task<PreExecutionResult> HandleReworkFlowAsync(PreExecutionContext context, string errorMessage)
    {
        logger.LogWarning("Требуется доработка: {ErrorMessage}", errorMessage);
        if (OnReworkRequired == null)
        {
            return PreExecutionResult.Fail("Обработчик доработки не настроен");
        }
        var flowResult = await OnReworkRequired(
            errorMessage,
            (adminUsername, reason) => ExecuteReworkRequestAsync(context, adminUsername, reason));
        if (flowResult.IsCancelled)
        {
            return PreExecutionResult.Cancelled(flowResult.ErrorMessage ?? errorMessage);
        }
        return PreExecutionResult.Continue();
    }

    private async Task<ReworkSubmitResult> ExecuteReworkRequestAsync(
        PreExecutionContext context,
        string adminUsername,
        string reason)
    {
        logger.LogInformation("Запрос на доработку: Admin={Admin}, Reason={Reason}", adminUsername, reason);
        var barcode = context.BarcodeValidation!.Barcode;
        var reworkResult = await operationStartService.ReworkAsync(barcode, CurrentOperatorName, adminUsername, reason);
        if (!reworkResult.IsSuccess)
        {
            return ReworkSubmitResult.Fail(reworkResult.ErrorMessage!);
        }
        logger.LogInformation("Доработка одобрена, повторный запрос в MES...");
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
}

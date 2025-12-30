using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.PreExecution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.Test;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;

public class PreExecutionCoordinator(
    IPreExecutionStepRegistry stepRegistry,
    TestExecutionCoordinator testCoordinator,
    ITestMapResolver mapResolver,
    StepStatusReporter statusReporter,
    BoilerState boilerState,
    IRecipeProvider recipeProvider,
    OpcUaTagService opcUa,
    ITestStepLogger testStepLogger,
    ILogger<PreExecutionCoordinator> logger)
{
    public async Task<PreExecutionResult> ExecuteAsync(string barcode, CancellationToken ct)
    {
        var context = CreateContext(barcode);
        var stepsResult = await ExecuteAllStepsAsync(context, ct);
        if (!stepsResult.Success)
        {
            return stepsResult;
        }
        var resolveResult = ResolveTestMaps(context);
        if (!resolveResult.Success)
        {
            return resolveResult;
        }
        StartTestExecution(context);
        return PreExecutionResult.Ok();
    }

    private PreExecutionResult ResolveTestMaps(PreExecutionContext context)
    {
        if (context.RawMaps == null || context.RawMaps.Count == 0)
        {
            return PreExecutionResult.Fail("Нет тестовых последовательностей");
        }
        var resolveResult = mapResolver.Resolve(context.RawMaps);
        if (resolveResult.UnknownSteps.Count > 0)
        {
            var error = $"Неизвестных шагов: {resolveResult.UnknownSteps.Count}";
            logger.LogWarning("{Error}", error);
            testStepLogger.LogWarning("{Error}", error);
            return PreExecutionResult.Fail(error, new UnknownStepsDetails(resolveResult.UnknownSteps));
        }
        context.Maps = resolveResult.Maps;
        return PreExecutionResult.Ok();
    }

    private async Task<PreExecutionResult> ExecuteAllStepsAsync(PreExecutionContext context, CancellationToken ct)
    {
        foreach (var step in stepRegistry.GetOrderedSteps())
        {
            var result = await ExecuteStepAsync(step, context, ct);
            if (!result.Success || result.ShouldStop)
            {
                return result;
            }
        }
        return PreExecutionResult.Ok();
    }

    private async Task<PreExecutionResult> ExecuteStepAsync(
        IPreExecutionStep step,
        PreExecutionContext context,
        CancellationToken ct)
    {
        var stepId = statusReporter.ReportStepStarted(step);
        try
        {
            return await ExecuteStepCoreAsync(step, stepId, context, ct);
        }
        catch (Exception ex)
        {
            return HandleStepException(step, stepId, ex);
        }
    }

    private async Task<PreExecutionResult> ExecuteStepCoreAsync(
        IPreExecutionStep step,
        Guid stepId,
        PreExecutionContext context,
        CancellationToken ct)
    {
        var result = await step.ExecuteAsync(context, ct);
        ReportStepResult(stepId, result);
        return result;
    }

    private void ReportStepResult(Guid stepId, PreExecutionResult result)
    {
        if (result.Success)
        {
            statusReporter.ReportSuccess(stepId);
            return;
        }
        statusReporter.ReportError(stepId, result.ErrorMessage!);
    }

    private PreExecutionResult HandleStepException(IPreExecutionStep step, Guid stepId, Exception ex)
    {
        logger.LogError(ex, "Ошибка в шаге {StepId}", step.Id);
        testStepLogger.LogError(ex, "Ошибка в шаге {StepId}", step.Id);
        statusReporter.ReportError(stepId, ex.Message);
        return PreExecutionResult.Fail(ex.Message);
    }

    private void StartTestExecution(PreExecutionContext context)
    {
        logger.LogInformation("Запуск TestExecutionCoordinator с {Count} maps", context.Maps?.Count ?? 0);
        testStepLogger.LogInformation("Запуск TestExecutionCoordinator с {Count} maps", context.Maps?.Count ?? 0);
        testCoordinator.SetMaps(context.Maps!);
        _ = testCoordinator.StartAsync();
    }

    private PreExecutionContext CreateContext(string barcode)
    {
        return new PreExecutionContext
        {
            Barcode = barcode,
            BoilerState = boilerState,
            RecipeProvider = recipeProvider,
            OpcUa = opcUa,
            TestStepLogger = testStepLogger
        };
    }
}

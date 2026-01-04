using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.PreExecution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.Recipe;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;

public class PreExecutionCoordinator(
    IPreExecutionStepRegistry stepRegistry,
    TestExecutionCoordinator testCoordinator,
    StepStatusReporter statusReporter,
    BoilerState boilerState,
    IRecipeProvider recipeProvider,
    OpcUaTagService opcUa,
    ITestStepLogger testStepLogger,
    ExecutionActivityTracker activityTracker,
    ExecutionMessageState messageState,
    PauseTokenSource pauseToken,
    ILogger<PreExecutionCoordinator> logger)
{
    public async Task<PreExecutionResult> ExecuteAsync(string barcode, CancellationToken ct)
    {
        activityTracker.SetPreExecutionActive(true);
        try
        {
            return await ExecutePreExecutionPipelineAsync(barcode, ct);
        }
        finally
        {
            activityTracker.SetPreExecutionActive(false);
        }
    }

    private async Task<PreExecutionResult> ExecutePreExecutionPipelineAsync(string barcode, CancellationToken ct)
    {
        statusReporter.ClearAll();
        var context = CreateContext(barcode);
        var stepsResult = await ExecuteAllStepsAsync(context, ct);
        if (!stepsResult.Success)
        {
            return HandleFailedResult(stepsResult);
        }
        messageState.Clear();
        StartTestExecution(context);
        return PreExecutionResult.Ok();
    }

    private PreExecutionResult HandleFailedResult(PreExecutionResult result)
    {
        if (result.UserMessage != null)
        {
            messageState.SetMessage(result.UserMessage);
        }
        return result;
    }

    private async Task<PreExecutionResult> ExecuteAllStepsAsync(PreExecutionContext context, CancellationToken ct)
    {
        foreach (var step in stepRegistry.GetOrderedSteps())
        {
            await pauseToken.WaitWhilePausedAsync(ct);

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
        LogTestExecutionStart(context);
        testCoordinator.SetMaps(context.Maps!);
        _ = testCoordinator.StartAsync();
    }

    private void LogTestExecutionStart(PreExecutionContext context)
    {
        var mapCount = context.Maps?.Count ?? 0;
        logger.LogInformation("Запуск TestExecutionCoordinator с {Count} maps", mapCount);
        testStepLogger.LogInformation("Запуск TestExecutionCoordinator с {Count} maps", mapCount);
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

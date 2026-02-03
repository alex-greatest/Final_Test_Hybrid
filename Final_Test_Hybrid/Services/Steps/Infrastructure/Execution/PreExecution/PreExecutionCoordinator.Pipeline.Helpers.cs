using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;
using Final_Test_Hybrid.Services.Steps.Steps;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;

public partial class PreExecutionCoordinator
{
    private static PreExecutionResult HandleNonContinueResult(PreExecutionResult result)
    {
        // UserMessage теперь обрабатывается через ErrorService/NotificationService
        return result;
    }

    private void InitializeTestRunning()
    {
        ClearForNewTestStart();
        AddAppVersionToResults();
        infra.ErrorService.IsHistoryEnabled = true;
        state.BoilerState.SetTestRunning(true);
        state.BoilerState.StartTestTimer();
        StopChangeoverAndAllowRestart();
    }

    private void ReportBlockStepResult(Guid stepId, PreExecutionResult result)
    {
        switch (result.Status)
        {
            case PreExecutionStatus.Continue:
                infra.StatusReporter.ReportSuccess(stepId, result.SuccessMessage ?? "", result.Limits);
                break;
            case PreExecutionStatus.Cancelled:
                // Не меняем статус - шаг остаётся с тем статусом, который был
                break;
            case PreExecutionStatus.TestStarted:
                break;
            case PreExecutionStatus.Failed:
                infra.StatusReporter.ReportError(stepId, result.ErrorMessage!, result.Limits);
                break;
            default:
                throw new InvalidOperationException($"Неизвестный статус PreExecution: {result.Status}");
        }
    }

    private PreExecutionResult HandleStepException(ScanStepBase step, Exception ex)
    {
        infra.Logger.LogError(ex, "Ошибка в шаге {StepId}", step.Id);
        infra.TestStepLogger.LogError(ex, "Ошибка в шаге {StepId}", step.Id);
        return PreExecutionResult.Fail(ex.Message);
    }

    private PreExecutionResult HandleStepException(BlockBoilerAdapterStep step, Guid stepId, Exception ex)
    {
        infra.Logger.LogError(ex, "Ошибка в шаге {StepId}", step.Id);
        infra.TestStepLogger.LogError(ex, "Ошибка в шаге {StepId}", step.Id);
        infra.StatusReporter.ReportError(stepId, ex.Message);
        return PreExecutionResult.Fail(ex.Message);
    }

    private bool StartTestExecution(PreExecutionContext context)
    {
        if (context.Maps == null || context.Maps.Count == 0)
        {
            LogStartFailure("Maps пусты");
            RollbackTestStart();
            return false;
        }
        LogTestExecutionStart(context);
        coordinators.TestCoordinator.SetMaps(context.Maps);
        if (!coordinators.TestCoordinator.TryStartInBackground())
        {
            LogStartFailure("TryStartInBackground вернул false");
            RollbackTestStart();
            return false;
        }
        return true;
    }

    private void LogStartFailure(string reason)
    {
        infra.Logger.LogWarning("Старт теста невозможен: {Reason}", reason);
    }

    /// <summary>
    /// Откатывает состояние теста при неудачном старте.
    /// Безопасен, т.к. откатывает флаги, установленные текущим pipeline.
    /// </summary>
    private void RollbackTestStart()
    {
        infra.Logger.LogInformation("Откат состояния теста");
        state.BoilerState.SetTestRunning(false);
        state.BoilerState.StopTestTimer();
    }

    private void LogTestExecutionStart(PreExecutionContext context)
    {
        var mapCount = context.Maps?.Count ?? 0;
        infra.Logger.LogInformation("Запуск TestExecutionCoordinator с {Count} maps", mapCount);
    }

    private PreExecutionContext CreateContext(string barcode)
    {
        return new PreExecutionContext
        {
            Barcode = barcode,
            BoilerState = state.BoilerState,
            OpcUa = infra.OpcUa,
            TestStepLogger = infra.TestStepLogger
        };
    }
}

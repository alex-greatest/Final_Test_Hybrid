using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.SpringBoot.Operation;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.PreExecution;
using Final_Test_Hybrid.Services.Steps.Steps;
using Final_Test_Hybrid.Services.Steps.Validation;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Scanning;

/// <summary>
/// Координирует диалоги ошибок PreExecution и rework-flow.
/// Делегирует UI-события компонентам (BoilerInfo).
/// </summary>
public class ScanDialogCoordinator
{
    private readonly ScanErrorHandler _errorHandler;

    public event Func<IReadOnlyList<string>, Task>? OnMissingPlcTagsDialogRequested;
    public event Func<IReadOnlyList<string>, Task>? OnMissingRequiredTagsDialogRequested;
    public event Func<IReadOnlyList<UnknownStepInfo>, Task>? OnUnknownStepsDialogRequested;
    public event Func<IReadOnlyList<MissingRecipeInfo>, Task>? OnMissingRecipesDialogRequested;
    public event Func<IReadOnlyList<RecipeWriteErrorInfo>, Task>? OnRecipeWriteErrorDialogRequested;
    public event Func<string, Func<string, string, Task<ReworkSubmitResult>>, Task<ReworkFlowResult>>? OnReworkDialogRequested;

    public ScanDialogCoordinator(
        ScanErrorHandler errorHandler,
        IEnumerable<IPreExecutionStep> preExecutionSteps)
    {
        _errorHandler = errorHandler;
        ConfigureReworkCallback(preExecutionSteps);
    }

    private void ConfigureReworkCallback(IEnumerable<IPreExecutionStep> steps)
    {
        var mesStep = steps.OfType<ScanBarcodeMesStep>().FirstOrDefault();
        if (mesStep != null)
        {
            mesStep.OnReworkRequired = HandleReworkDialogAsync;
        }
    }

    private async Task<ReworkFlowResult> HandleReworkDialogAsync(
        string errorMessage,
        Func<string, string, Task<ReworkSubmitResult>> executeRework)
    {
        if (OnReworkDialogRequested == null)
        {
            return ReworkFlowResult.Cancelled();
        }
        return await OnReworkDialogRequested(errorMessage, executeRework);
    }

    public async Task HandlePreExecutionErrorAsync(PreExecutionResult result)
    {
        _errorHandler.ShowError("Ошибка", result.ErrorMessage ?? "Неизвестная ошибка");
        await RaiseDetailedErrorDialogAsync(result);
    }

    private async Task RaiseDetailedErrorDialogAsync(PreExecutionResult result)
    {
        var task = result.ErrorDetails switch
        {
            MissingPlcTagsDetails details => OnMissingPlcTagsDialogRequested?.Invoke(details.Tags),
            MissingRequiredTagsDetails details => OnMissingRequiredTagsDialogRequested?.Invoke(details.Tags),
            UnknownStepsDetails details => OnUnknownStepsDialogRequested?.Invoke(details.Steps),
            MissingRecipesDetails details => OnMissingRecipesDialogRequested?.Invoke(details.Recipes),
            RecipeWriteErrorDetails details => OnRecipeWriteErrorDialogRequested?.Invoke(details.Errors),
            _ => null
        };
        await (task ?? Task.CompletedTask);
    }

    public void ShowCompletionNotification(bool hasErrors)
    {
        if (hasErrors)
        {
            _errorHandler.ShowError("Тест завершён", "Выполнение прервано из-за ошибки");
            return;
        }
        _errorHandler.ShowSuccess("Тест завершён", "Все шаги выполнены успешно");
    }
}

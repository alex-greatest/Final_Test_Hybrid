using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.SpringBoot.Operation;
using Final_Test_Hybrid.Services.SpringBoot.Operation.Interrupt;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;
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
    public event Func<string, string, string, Task>? OnBlockErrorDialogRequested;
    public event Action? OnBlockErrorDialogCloseRequested;
    public event Func<string, Func<string, string, CancellationToken, Task<SaveResult>>, bool, bool, string, CancellationToken, Task<InterruptFlowResult>>? OnInterruptReasonDialogRequested;

    public ScanDialogCoordinator(
        ScanErrorHandler errorHandler,
        ScanBarcodeMesStep scanBarcodeMesStep)
    {
        _errorHandler = errorHandler;
        scanBarcodeMesStep.OnReworkRequired = HandleReworkDialogAsync;
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

    public Task ShowBlockErrorDialogAsync(string stepName, string errorMessage, string errorSourceTitle)
    {
        // Fire-and-forget: диалог показывается, но не блокирует выполнение
        _ = OnBlockErrorDialogRequested?.Invoke(stepName, errorMessage, errorSourceTitle);
        return Task.CompletedTask;
    }

    public void CloseBlockErrorDialog()
    {
        OnBlockErrorDialogCloseRequested?.Invoke();
    }

    public async Task<InterruptFlowResult> ShowInterruptReasonDialogAsync(
        string serialNumber,
        Func<string, string, CancellationToken, Task<SaveResult>> onSave,
        bool useMes,
        bool requireAdminAuth,
        string operatorUsername,
        CancellationToken ct)
    {
        if (OnInterruptReasonDialogRequested == null)
        {
            return InterruptFlowResult.Cancelled();
        }
        return await OnInterruptReasonDialogRequested(serialNumber, onSave, useMes, requireAdminAuth, operatorUsername, ct);
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

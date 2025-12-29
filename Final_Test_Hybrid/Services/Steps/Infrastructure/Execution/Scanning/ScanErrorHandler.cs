using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common.UI;
using Final_Test_Hybrid.Services.Steps.Validation;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Scanning;

public class ScanErrorHandler(
    INotificationService notificationService,
    TestSequenseService sequenseService,
    ILogger<ScanErrorHandler> logger)
{
    public event Func<IReadOnlyList<string>, Task>? OnMissingPlcTagsDialogRequested;
    public event Func<IReadOnlyList<string>, Task>? OnMissingRequiredTagsDialogRequested;
    public event Func<IReadOnlyList<UnknownStepInfo>, Task>? OnUnknownStepsDialogRequested;
    public event Func<IReadOnlyList<MissingRecipeInfo>, Task>? OnMissingRecipesDialogRequested;

    public async Task HandleResultAsync(BarcodeProcessingResult result)
    {
        if (result.IsSuccess)
        {
            return;
        }
        await HandleErrorResult(result);
    }

    private async Task HandleErrorResult(BarcodeProcessingResult result)
    {
        await HandleMissingPlcTags(result.MissingPlcTags);
        await HandleMissingRequiredTags(result.MissingRequiredTags);
        await HandleUnknownSteps(result.UnknownSteps);
        await HandleMissingRecipes(result.MissingRecipes);
        ReportError(result.ErrorMessage!, result.ScanStepId);
    }

    private async Task HandleMissingPlcTags(IReadOnlyList<string>? tags)
    {
        if (tags == null || tags.Count == 0)
        {
            return;
        }
        notificationService.ShowWarning("Внимание", $"Обнаружено {tags.Count} отсутствующих тегов для PLC");
        await InvokeDialogHandler(OnMissingPlcTagsDialogRequested, tags);
    }

    private async Task HandleMissingRequiredTags(IReadOnlyList<string>? tags)
    {
        if (tags == null || tags.Count == 0)
        {
            return;
        }
        notificationService.ShowWarning("Внимание", $"Обнаружено {tags.Count} обязательных тегов, отсутствующих в рецептах");
        await InvokeDialogHandler(OnMissingRequiredTagsDialogRequested, tags);
    }

    private async Task HandleUnknownSteps(IReadOnlyList<UnknownStepInfo>? unknownSteps)
    {
        if (unknownSteps == null || unknownSteps.Count == 0)
        {
            return;
        }
        notificationService.ShowWarning("Внимание", $"Обнаружено {unknownSteps.Count} неизвестных шагов в последовательности");
        await InvokeDialogHandler(OnUnknownStepsDialogRequested, unknownSteps);
    }

    private async Task HandleMissingRecipes(IReadOnlyList<MissingRecipeInfo>? missingRecipes)
    {
        if (missingRecipes == null || missingRecipes.Count == 0)
        {
            return;
        }
        notificationService.ShowWarning("Внимание", $"Обнаружено {missingRecipes.Count} отсутствующих рецептов");
        await InvokeDialogHandler(OnMissingRecipesDialogRequested, missingRecipes);
    }

    public void ReportError(string error, Guid scanStepId)
    {
        logger.LogError("Scan error: {Error}", error);
        notificationService.ShowError("Ошибка", error);
        sequenseService.SetError(scanStepId, error);
    }

    public void ShowSuccess(string title, string message)
    {
        notificationService.ShowSuccess(title, message);
    }

    public void ShowError(string title, string message)
    {
        notificationService.ShowError(title, message);
    }

    private static async Task InvokeDialogHandler<T>(
        Func<IReadOnlyList<T>, Task>? handler,
        IReadOnlyList<T> items)
    {
        if (handler == null)
        {
            return;
        }
        await handler.Invoke(items);
    }
}

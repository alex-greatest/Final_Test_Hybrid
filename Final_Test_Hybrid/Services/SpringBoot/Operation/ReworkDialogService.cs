using Final_Test_Hybrid.Components.Main.Modals.Rework;
using Radzen;

namespace Final_Test_Hybrid.Services.SpringBoot.Operation;

public class ReworkDialogService(DialogService dialogService)
{
    public async Task<bool> ShowRouteErrorAsync(string message)
    {
        var result = await dialogService.OpenAsync<RouteErrorDialog>(
            "",
            new Dictionary<string, object> { ["Message"] = message },
            CreateModalOptions("500px", showTitle: false));
        return result is true;
    }

    public async Task<AdminAuthResult?> ShowAdminAuthAsync()
    {
        var result = await dialogService.OpenAsync<AdminAuthDialog>(
            "Авторизация администратора",
            new Dictionary<string, object>(),
            CreateModalOptions("400px"));
        return result as AdminAuthResult;
    }

    public async Task<string?> ShowReworkReasonAsync()
    {
        var result = await dialogService.OpenAsync<ReworkReasonDialog>(
            "Доработка/пропуск",
            new Dictionary<string, object>(),
            CreateModalOptions("550px"));
        return result as string;
    }

    public async Task<ReworkSubmitResult?> ShowReworkReasonAsync(
        Func<string, Task<ReworkSubmitResult>> onSubmit)
    {
        var result = await dialogService.OpenAsync<ReworkReasonDialog>(
            "Доработка/пропуск",
            new Dictionary<string, object> { ["OnSubmit"] = onSubmit },
            CreateModalOptions("550px"));
        return result as ReworkSubmitResult;
    }

    public async Task<ReworkFlowResult> ExecuteReworkFlowAsync(string errorMessage)
    {
        var authResult = await ValidateAndAuthenticateAsync(errorMessage);
        if (authResult == null)
        {
            return ReworkFlowResult.Cancelled();
        }
        var reason = await ShowReworkReasonAsync();
        return string.IsNullOrEmpty(reason) ? ReworkFlowResult.Cancelled() : ReworkFlowResult.Success(authResult.Username);
    }

    public async Task<ReworkFlowResult> ExecuteReworkFlowAsync(
        string errorMessage,
        Func<string, string, Task<ReworkSubmitResult>> executeRework)
    {
        var authResult = await ValidateAndAuthenticateAsync(errorMessage);
        if (authResult == null)
        {
            return ReworkFlowResult.Cancelled();
        }
        var submitResult = await ShowReworkReasonAsync(
            reason => executeRework(authResult.Username, reason));
        return submitResult is not { IsSuccess: true } ? ReworkFlowResult.Cancelled() : ReworkFlowResult.Success(authResult.Username, submitResult.Data);
    }

    private async Task<AdminAuthResult?> ValidateAndAuthenticateAsync(string errorMessage)
    {
        var wantsToContinue = await ShowRouteErrorAsync(errorMessage);
        if (!wantsToContinue)
        {
            return null;
        }
        var authResult = await ShowAdminAuthAsync();
        return authResult is { Success: true } ? authResult : null;
    }

    private static DialogOptions CreateModalOptions(string width, bool showTitle = true)
    {
        return new DialogOptions
        {
            Width = width,
            ShowTitle = showTitle,
            CloseDialogOnOverlayClick = false,
            CloseDialogOnEsc = false
        };
    }
}

public class ReworkFlowResult
{
    public bool IsSuccess { get; init; }
    public bool IsCancelled { get; init; }
    public string AdminUsername { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public object? Data { get; init; }

    public static ReworkFlowResult Success(string adminUsername, object? data = null) =>
        new() { IsSuccess = true, AdminUsername = adminUsername, Data = data };

    public static ReworkFlowResult Cancelled() =>
        new() { IsCancelled = true };
}

public class ReworkSubmitResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public object? Data { get; init; }

    public static ReworkSubmitResult Success(object? data = null) =>
        new() { IsSuccess = true, Data = data };

    public static ReworkSubmitResult Fail(string error) =>
        new() { IsSuccess = false, ErrorMessage = error };
}

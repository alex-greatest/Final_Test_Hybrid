using Final_Test_Hybrid.Components.Main.Modals.Rework;
using Radzen;

namespace Final_Test_Hybrid.Services.SpringBoot.Operation;

public class ReworkDialogService(DialogService dialogService)
{
    private string? _lastError;

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
            CreateModalOptions("450px"));
        return result as AdminAuthResult;
    }

    public async Task<string?> ShowReworkReasonAsync()
    {
        var result = await dialogService.OpenAsync<ReworkReasonDialog>(
            "Доработка/пропуск",
            new Dictionary<string, object>(),
            CreateModalOptions("85vw"));
        return result as string;
    }

    public async Task<ReworkSubmitResult?> ShowReworkReasonAsync(
        Func<string, Task<ReworkSubmitResult>> onSubmit)
    {
        var result = await dialogService.OpenAsync<ReworkReasonDialog>(
            "Доработка/пропуск",
            new Dictionary<string, object> { ["OnSubmit"] = onSubmit },
            CreateModalOptions("85vw"));
        return result as ReworkSubmitResult;
    }

    public async Task<ReworkFlowResult> ExecuteReworkFlowAsync(string errorMessage)
    {
        _lastError = errorMessage;
        var wantsToContinue = await ShowRouteErrorAsync(errorMessage);
        if (!wantsToContinue)
        {
            return ReworkFlowResult.Cancelled(_lastError);
        }
        return await ProcessAuthAndReasonAsync();
    }

    public async Task<ReworkFlowResult> ExecuteReworkFlowAsync(
        string errorMessage,
        Func<string, string, Task<ReworkSubmitResult>> executeRework)
    {
        _lastError = errorMessage;
        var wantsToContinue = await ShowRouteErrorAsync(errorMessage);
        if (!wantsToContinue)
        {
            return ReworkFlowResult.Cancelled(_lastError);
        }
        return await ProcessAuthAndSubmitAsync(executeRework);
    }

    private async Task<ReworkFlowResult> ProcessAuthAndReasonAsync()
    {
        var authResult = await AuthenticateAdminAsync();
        if (!authResult.Success)
        {
            return ReworkFlowResult.Cancelled(_lastError);
        }
        return await GetReasonAndCompleteFlowAsync(authResult.Username);
    }

    private async Task<ReworkFlowResult> ProcessAuthAndSubmitAsync(
        Func<string, string, Task<ReworkSubmitResult>> executeRework)
    {
        var authResult = await AuthenticateAdminAsync();
        if (!authResult.Success)
        {
            return ReworkFlowResult.Cancelled(_lastError);
        }
        return await SubmitReworkAsync(authResult.Username, executeRework);
    }

    private async Task<(bool Success, string Username)> AuthenticateAdminAsync()
    {
        var authResult = await ShowAdminAuthAsync();
        if (authResult is { Success: true })
        {
            return (true, authResult.Username);
        }
        UpdateLastErrorFromAuth(authResult);
        return (false, string.Empty);
    }

    private void UpdateLastErrorFromAuth(AdminAuthResult? authResult)
    {
        if (authResult?.ErrorMessage != null)
        {
            _lastError = authResult.ErrorMessage;
        }
    }

    private async Task<ReworkFlowResult> GetReasonAndCompleteFlowAsync(string username)
    {
        var reason = await ShowReworkReasonAsync();
        if (string.IsNullOrEmpty(reason))
        {
            return ReworkFlowResult.Cancelled(_lastError);
        }
        return ReworkFlowResult.Success(username);
    }

    private async Task<ReworkFlowResult> SubmitReworkAsync(
        string username,
        Func<string, string, Task<ReworkSubmitResult>> executeRework)
    {
        var submitResult = await ShowReworkReasonAsync(reason => executeRework(username, reason));
        if (submitResult is { IsSuccess: true })
        {
            return ReworkFlowResult.Success(username, submitResult.Data);
        }
        UpdateLastErrorFromSubmit(submitResult);
        return ReworkFlowResult.Cancelled(_lastError);
    }

    private void UpdateLastErrorFromSubmit(ReworkSubmitResult? submitResult)
    {
        if (submitResult?.ErrorMessage != null)
        {
            _lastError = submitResult.ErrorMessage;
        }
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
    public string? ErrorMessage { get; init; }

    public static ReworkFlowResult Success(string adminUsername, object? data = null) =>
        new() { IsSuccess = true, AdminUsername = adminUsername, Data = data };

    public static ReworkFlowResult Cancelled(string? errorMessage = null) =>
        new() { IsCancelled = true, ErrorMessage = errorMessage };
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

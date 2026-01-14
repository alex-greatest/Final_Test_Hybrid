using Final_Test_Hybrid.Components.Main.Modals.Rework;
using Final_Test_Hybrid.Services.Main.PlcReset;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator;
using Radzen;

namespace Final_Test_Hybrid.Services.SpringBoot.Operation;

public class ReworkDialogService : IDisposable
{
    private readonly DialogService _dialogService;
    private readonly PlcResetCoordinator _plcReset;
    private readonly IErrorCoordinator _errorCoordinator;
    private string? _lastError;

    public ReworkDialogService(
        DialogService dialogService,
        PlcResetCoordinator plcReset,
        IErrorCoordinator errorCoordinator)
    {
        _dialogService = dialogService;
        _plcReset = plcReset;
        _errorCoordinator = errorCoordinator;

        _plcReset.OnForceStop += HandleForceStop;
        _errorCoordinator.OnReset += HandleReset;
    }

    private void HandleForceStop() => _dialogService.Close();
    private void HandleReset() => _dialogService.Close();

    public async Task<bool> ShowRouteErrorAsync(string message)
    {
        var result = await _dialogService.OpenAsync<RouteErrorDialog>(
            "",
            new Dictionary<string, object> { ["Message"] = message },
            CreateModalOptions("500px", showTitle: false));
        return result is true;
    }

    public async Task<AdminAuthResult?> ShowAdminAuthAsync()
    {
        var result = await _dialogService.OpenAsync<AdminAuthDialog>(
            "Авторизация администратора",
            new Dictionary<string, object>(),
            CreateModalOptions("450px"));
        return result as AdminAuthResult;
    }

    public async Task<string?> ShowReworkReasonAsync()
    {
        var result = await _dialogService.OpenAsync<ReworkReasonDialog>(
            "Доработка/пропуск",
            new Dictionary<string, object>(),
            CreateModalOptions("85vw"));
        return result as string;
    }

    public async Task<ReworkSubmitResult?> ShowReworkReasonAsync(
        Func<string, Task<ReworkSubmitResult>> onSubmit)
    {
        var result = await _dialogService.OpenAsync<ReworkReasonDialog>(
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
        var authResult = await ShowAdminAuthAsync();
        if (authResult is not { Success: true })
        {
            _lastError = authResult?.ErrorMessage ?? _lastError;
            return ReworkFlowResult.Cancelled(_lastError);
        }

        var reason = await ShowReworkReasonAsync();
        return string.IsNullOrEmpty(reason) ? ReworkFlowResult.Cancelled(_lastError) : ReworkFlowResult.Success(authResult.Username);
    }

    private async Task<ReworkFlowResult> ProcessAuthAndSubmitAsync(
        Func<string, string, Task<ReworkSubmitResult>> executeRework)
    {
        var authResult = await ShowAdminAuthAsync();
        if (authResult is not { Success: true })
        {
            _lastError = authResult?.ErrorMessage ?? _lastError;
            return ReworkFlowResult.Cancelled(_lastError);
        }

        var submitResult = await ShowReworkReasonAsync(reason => executeRework(authResult.Username, reason));
        if (submitResult is { IsSuccess: true })
        {
            return ReworkFlowResult.Success(authResult.Username, submitResult.Data);
        }
        _lastError = submitResult?.ErrorMessage ?? _lastError;
        return ReworkFlowResult.Cancelled(_lastError);
    }

    private static DialogOptions CreateModalOptions(string width, bool showTitle = true)
    {
        return new DialogOptions
        {
            Width = width,
            ShowTitle = showTitle,
            ShowClose = false,
            CloseDialogOnOverlayClick = false,
            CloseDialogOnEsc = false
        };
    }

    public void Dispose()
    {
        _plcReset.OnForceStop -= HandleForceStop;
        _errorCoordinator.OnReset -= HandleReset;
    }
}

public class ReworkFlowResult
{
    public bool IsSuccess { get; init; }
    public bool IsCancelled { get; init; }
    public string AdminUsername { get; init; } = string.Empty;
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

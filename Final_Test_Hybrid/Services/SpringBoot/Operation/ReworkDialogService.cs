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
            new DialogOptions
            {
                Width = "500px",
                ShowTitle = false,
                CloseDialogOnOverlayClick = false,
                CloseDialogOnEsc = false
            });
        return result is true;
    }

    public async Task<AdminAuthResult?> ShowAdminAuthAsync()
    {
        var result = await dialogService.OpenAsync<AdminAuthDialog>(
            "Авторизация администратора",
            new Dictionary<string, object>(),
            new DialogOptions
            {
                Width = "400px",
                CloseDialogOnOverlayClick = false,
                CloseDialogOnEsc = false
            });
        return result as AdminAuthResult;
    }

    public async Task<string?> ShowReworkReasonAsync()
    {
        var result = await dialogService.OpenAsync<ReworkReasonDialog>(
            "Доработка/пропуск",
            new Dictionary<string, object>(),
            new DialogOptions
            {
                Width = "550px",
                CloseDialogOnOverlayClick = false,
                CloseDialogOnEsc = false
            });
        return result as string;
    }

    public async Task<ReworkFlowResult> ExecuteReworkFlowAsync(string errorMessage)
    {
        var wantsToContinue = await ShowRouteErrorAsync(errorMessage);
        if (!wantsToContinue)
        {
            return ReworkFlowResult.Cancelled();
        }
        var authResult = await ShowAdminAuthAsync();
        if (authResult is not { Success: true })
        {
            return ReworkFlowResult.Cancelled();
        }
        var reason = await ShowReworkReasonAsync();
        return string.IsNullOrEmpty(reason) ? ReworkFlowResult.Cancelled() : ReworkFlowResult.Success(authResult.Username, reason);
    }
}

public class ReworkFlowResult
{
    public bool IsSuccess { get; init; }
    public bool IsCancelled { get; init; }
    public string AdminUsername { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;

    public static ReworkFlowResult Success(string adminUsername, string reason) =>
        new() { IsSuccess = true, AdminUsername = adminUsername, Reason = reason };

    public static ReworkFlowResult Cancelled() =>
        new() { IsCancelled = true };
}

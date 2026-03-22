using Final_Test_Hybrid.Components.Main.Modals.Interrupt;
using Final_Test_Hybrid.Components.Main.Modals.Rework;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Final_Test_Hybrid.Services.SpringBoot.Operation.Interrupt;

/// <summary>
/// Оркестрация flow авторизации и ввода причины.
/// Создаётся per-request, НЕ singleton.
/// </summary>
public class InterruptDialogService(DialogService dialogService, Action? closeOverride = null)
{
    private readonly Action _closeAction = closeOverride ?? (() => dialogService.Close());

    public virtual Task<AdminAuthResult?> ShowAdminAuthAsync()
    {
        var parameters = new Dictionary<string, object>
        {
            ["ShowCancelButton"] = true,
            ["RequireProtectedCancel"] = true
        };
        return ShowDialogAsync<AdminAuthDialog, AdminAuthResult>(
            "Авторизация администратора", "450px", parameters);
    }

    public virtual Task<SaveResult?> ShowInterruptReasonAsync(
        Func<string, CancellationToken, Task<SaveResult>> onSubmit,
        CancellationToken ct)
    {
        var parameters = new Dictionary<string, object>
        {
            ["OnSubmit"] = onSubmit,
            ["CancellationToken"] = ct
        };
        return ShowDialogAsync<InterruptReasonDialog, SaveResult>(
            "Причина прерывания", "85vw", parameters);
    }

    public virtual void CloseDialog()
    {
        _closeAction();
    }

    private async Task<TResult?> ShowDialogAsync<TDialog, TResult>(
        string title, string width, Dictionary<string, object>? parameters = null)
        where TDialog : ComponentBase
    {
        var options = new DialogOptions
        {
            Width = width,
            CloseDialogOnEsc = false,
            CloseDialogOnOverlayClick = false,
            ShowClose = false
        };
        var result = await dialogService.OpenAsync<TDialog>(
            title, parameters ?? [], options);
        return result is TResult typed ? typed : default;
    }
}

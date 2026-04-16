using Final_Test_Hybrid.Components.Main.Modals.Rework;

namespace Final_Test_Hybrid.Services.SpringBoot.Operation.Interrupt;

/// <summary>
/// Координатор flow авторизации и ввода причины прерывания.
/// Создаётся per-request, НЕ singleton.
/// </summary>
public class InterruptFlowExecutor
{
    /// <summary>
    /// Выполняет flow ввода причины прерывания.
    /// </summary>
    /// <param name="dialogService">Сервис диалогов.</param>
    /// <param name="saveCallback">Callback для сохранения (admin, reason, ct).</param>
    /// <param name="requireAdminAuth">Нужно ли показывать авторизацию администратора перед вводом причины.</param>
    /// <param name="operatorUsername">Имя оператора для flow без авторизации.</param>
    /// <param name="showCancelButton">Нужно ли показывать локальную кнопку отмены в auth/reason dialog.</param>
    /// <param name="allowRepeatBypassOnCancel">Разрешён ли аварийный repeat-bypass через cancel outcome.</param>
    /// <param name="ct">Токен отмены.</param>
    public async Task<InterruptFlowResult> ExecuteAsync(
        InterruptDialogService dialogService,
        Func<string, string, CancellationToken, Task<SaveResult>> saveCallback,
        bool requireAdminAuth,
        string operatorUsername,
        bool showCancelButton,
        bool allowRepeatBypassOnCancel,
        CancellationToken ct)
    {
        if (IsCancelled(ct))
        {
            return InterruptFlowResult.Cancelled();
        }
        using var registration = ct.Register(dialogService.CloseDialog);
        return requireAdminAuth
            ? await ExecuteFullFlowAsync(
                dialogService,
                saveCallback,
                showCancelButton,
                allowRepeatBypassOnCancel,
                ct)
            : await ExecuteSimpleFlowAsync(
                dialogService,
                saveCallback,
                operatorUsername,
                showCancelButton,
                allowRepeatBypassOnCancel,
                ct);
    }

    /// <summary>
    /// Полный flow: авторизация администратора + причина.
    /// </summary>
    private async Task<InterruptFlowResult> ExecuteFullFlowAsync(
        InterruptDialogService dialogService,
        Func<string, string, CancellationToken, Task<SaveResult>> saveCallback,
        bool showCancelButton,
        bool allowRepeatBypassOnCancel,
        CancellationToken ct)
    {
        var authResult = await ExecuteAuthStepAsync(
            dialogService,
            showCancelButton,
            allowRepeatBypassOnCancel,
            ct);
        return await ContinueWithReasonStepAsync(
            dialogService,
            saveCallback,
            authResult,
            showCancelButton,
            allowRepeatBypassOnCancel,
            ct);
    }

    /// <summary>
    /// Упрощённый flow: только причина, используем имя оператора.
    /// </summary>
    private Task<InterruptFlowResult> ExecuteSimpleFlowAsync(
        InterruptDialogService dialogService,
        Func<string, string, CancellationToken, Task<SaveResult>> saveCallback,
        string operatorUsername,
        bool showCancelButton,
        bool allowRepeatBypassOnCancel,
        CancellationToken ct)
    {
        return ExecuteReasonStepAsync(
            dialogService,
            saveCallback,
            operatorUsername,
            showCancelButton,
            allowRepeatBypassOnCancel,
            ct);
    }

    private Task<InterruptFlowResult> ContinueWithReasonStepAsync(
        InterruptDialogService dialogService,
        Func<string, string, CancellationToken, Task<SaveResult>> saveCallback,
        AdminAuthResult? authResult,
        bool showCancelButton,
        bool allowRepeatBypassOnCancel,
        CancellationToken ct)
    {
        if (authResult?.IsRepeatBypass == true)
        {
            return Task.FromResult(InterruptFlowResult.RepeatBypass());
        }

        return authResult == null
            ? Task.FromResult(InterruptFlowResult.Cancelled())
            : ExecuteReasonStepAsync(
                dialogService,
                saveCallback,
                authResult.Username,
                showCancelButton,
                allowRepeatBypassOnCancel,
                ct);
    }

    private async Task<AdminAuthResult?> ExecuteAuthStepAsync(
        InterruptDialogService dialogService,
        bool showCancelButton,
        bool allowRepeatBypassOnCancel,
        CancellationToken ct)
    {
        if (IsCancelled(ct))
        {
            return null;
        }
        var result = await dialogService.ShowAdminAuthAsync(
            showCancelButton,
            requireProtectedCancel: showCancelButton,
            returnRepeatBypassOnCancel: allowRepeatBypassOnCancel);
        return IsCancelled(ct) ? null : result;
    }

    private async Task<InterruptFlowResult> ExecuteReasonStepAsync(
        InterruptDialogService dialogService,
        Func<string, string, CancellationToken, Task<SaveResult>> saveCallback,
        string adminUsername,
        bool showCancelButton,
        bool allowRepeatBypassOnCancel,
        CancellationToken ct)
    {
        if (IsCancelled(ct))
        {
            return InterruptFlowResult.Cancelled();
        }
        var submitResult = await ShowReasonDialogAsync(
            dialogService,
            saveCallback,
            adminUsername,
            showCancelButton,
            allowRepeatBypassOnCancel,
            ct);
        return BuildReasonResult(submitResult, adminUsername, ct);
    }

    private static async Task<InterruptReasonDialogResult?> ShowReasonDialogAsync(
        InterruptDialogService dialogService,
        Func<string, string, CancellationToken, Task<SaveResult>> saveCallback,
        string adminUsername,
        bool showCancelButton,
        bool allowRepeatBypassOnCancel,
        CancellationToken ct)
    {
        return await dialogService.ShowInterruptReasonAsync(
            (reason, token) => saveCallback(adminUsername, reason, token),
            ct,
            showCancelButton,
            allowRepeatBypassOnCancel);
    }

    private static InterruptFlowResult BuildReasonResult(
        InterruptReasonDialogResult? submitResult,
        string adminUsername,
        CancellationToken ct)
    {
        if (IsCancelled(ct))
        {
            return InterruptFlowResult.Cancelled();
        }

        if (submitResult?.IsRepeatBypass == true)
        {
            return InterruptFlowResult.RepeatBypass();
        }

        return submitResult?.SaveResult is { IsSuccess: true }
            ? InterruptFlowResult.Success(adminUsername)
            : InterruptFlowResult.Cancelled();
    }

    private static bool IsCancelled(CancellationToken ct) => ct.IsCancellationRequested;
}

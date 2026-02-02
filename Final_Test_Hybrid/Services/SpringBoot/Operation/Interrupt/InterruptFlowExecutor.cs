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
    /// <param name="useMes">Режим MES (true = с авторизацией админа).</param>
    /// <param name="operatorUsername">Имя оператора для режима БД.</param>
    /// <param name="ct">Токен отмены.</param>
    public async Task<InterruptFlowResult> ExecuteAsync(
        InterruptDialogService dialogService,
        Func<string, string, CancellationToken, Task<SaveResult>> saveCallback,
        bool useMes,
        string operatorUsername,
        CancellationToken ct)
    {
        if (IsCancelled(ct))
        {
            return InterruptFlowResult.Cancelled();
        }
        using var registration = ct.Register(dialogService.CloseDialog);
        return useMes
            ? await ExecuteFullFlowAsync(dialogService, saveCallback, ct)
            : await ExecuteSimpleFlowAsync(dialogService, saveCallback, operatorUsername, ct);
    }

    /// <summary>
    /// Полный flow (MES): авторизация + причина.
    /// </summary>
    private async Task<InterruptFlowResult> ExecuteFullFlowAsync(
        InterruptDialogService dialogService,
        Func<string, string, CancellationToken, Task<SaveResult>> saveCallback,
        CancellationToken ct)
    {
        var authResult = await ExecuteAuthStepAsync(dialogService, ct);
        return await ContinueWithReasonStepAsync(dialogService, saveCallback, authResult, ct);
    }

    /// <summary>
    /// Упрощённый flow (БД): только причина, используем имя оператора.
    /// </summary>
    private Task<InterruptFlowResult> ExecuteSimpleFlowAsync(
        InterruptDialogService dialogService,
        Func<string, string, CancellationToken, Task<SaveResult>> saveCallback,
        string operatorUsername,
        CancellationToken ct)
    {
        return ExecuteReasonStepAsync(dialogService, saveCallback, operatorUsername, ct);
    }

    private Task<InterruptFlowResult> ContinueWithReasonStepAsync(
        InterruptDialogService dialogService,
        Func<string, string, CancellationToken, Task<SaveResult>> saveCallback,
        AdminAuthResult? authResult,
        CancellationToken ct)
    {
        return authResult == null
            ? Task.FromResult(InterruptFlowResult.Cancelled())
            : ExecuteReasonStepAsync(dialogService, saveCallback, authResult.Username, ct);
    }

    private async Task<AdminAuthResult?> ExecuteAuthStepAsync(
        InterruptDialogService dialogService,
        CancellationToken ct)
    {
        if (IsCancelled(ct))
        {
            return null;
        }
        var result = await dialogService.ShowAdminAuthAsync();
        return IsCancelled(ct) ? null : result;
    }

    private async Task<InterruptFlowResult> ExecuteReasonStepAsync(
        InterruptDialogService dialogService,
        Func<string, string, CancellationToken, Task<SaveResult>> saveCallback,
        string adminUsername,
        CancellationToken ct)
    {
        if (IsCancelled(ct))
        {
            return InterruptFlowResult.Cancelled();
        }
        var submitResult = await ShowReasonDialogAsync(dialogService, saveCallback, adminUsername, ct);
        return BuildReasonResult(submitResult, adminUsername, ct);
    }

    private static async Task<SaveResult?> ShowReasonDialogAsync(
        InterruptDialogService dialogService,
        Func<string, string, CancellationToken, Task<SaveResult>> saveCallback,
        string adminUsername,
        CancellationToken ct)
    {
        return await dialogService.ShowInterruptReasonAsync(
            (reason, token) => saveCallback(adminUsername, reason, token),
            ct);
    }

    private static InterruptFlowResult BuildReasonResult(
        SaveResult? submitResult,
        string adminUsername,
        CancellationToken ct)
    {
        if (IsCancelled(ct))
        {
            return InterruptFlowResult.Cancelled();
        }
        return submitResult is { IsSuccess: true }
            ? InterruptFlowResult.Success(adminUsername)
            : InterruptFlowResult.Cancelled();
    }

    private static bool IsCancelled(CancellationToken ct) => ct.IsCancellationRequested;
}

using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Services.Errors;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Components.Errors;

public partial class FloatingErrorBadgeHost : ComponentBase, IAsyncDisposable
{
    private const int MaxBlinkRestartAttempts = 5;
    private const string BadgeElementId = "floating-error-badge";
    private const string PanelElementId = "floating-active-errors-window";

    [Inject] private IErrorService ErrorService { get; set; } = null!;
    [Inject] private AppSettingsService AppSettingsService { get; set; } = null!;
    [Inject] private IJSRuntime JsRuntime { get; set; } = null!;
    [Inject] private ILogger<FloatingErrorBadgeHost> Logger { get; set; } = null!;

    private bool _disposed;
    private bool _isPanelOpen;
    private bool _pendingBlinkRestart;
    private bool _wasBadgeVisible;
    private int _blinkRestartAttemptCount;
    private int _resettableErrorsCount;

    private bool ShouldShowBadge => AppSettingsService.UseFloatingErrorBadge && _resettableErrorsCount > 0;
    private bool ShouldShowPanel => ShouldShowBadge && _isPanelOpen;
    private string BadgeCssVariables =>
        $"--floating-error-badge-width: {AppSettingsService.FloatingErrorBadge.WidthPx}px;" +
        $" --floating-error-badge-height: {AppSettingsService.FloatingErrorBadge.HeightPx}px;" +
        $" --floating-error-badge-icon-size: {AppSettingsService.FloatingErrorBadge.IconSizePx}px;" +
        $" --floating-error-badge-counter-min-width: {AppSettingsService.FloatingErrorBadge.CounterMinWidthPx}px;" +
        $" --floating-error-badge-counter-height: {AppSettingsService.FloatingErrorBadge.CounterHeightPx}px;" +
        $" --floating-error-badge-counter-font-size: {AppSettingsService.FloatingErrorBadge.CounterFontSizePx}px;";

    protected override void OnInitialized()
    {
        ErrorService.OnActiveErrorsChanged += OnErrorsChanged;
        RefreshState();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_pendingBlinkRestart || !ShouldShowBadge)
        {
            return;
        }

        if (_blinkRestartAttemptCount >= MaxBlinkRestartAttempts)
        {
            _pendingBlinkRestart = false;
            return;
        }

        try
        {
            await JsRuntime.InvokeVoidAsync("floatingPanel.restartAnimation", BadgeElementId);
            _pendingBlinkRestart = false;
            _blinkRestartAttemptCount = 0;
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            await RetryBlinkRestartAsync(ex);
        }
    }

    private void OnErrorsChanged()
    {
        _ = InvokeAsync(() =>
        {
            if (_disposed)
            {
                return;
            }
            RefreshState();
            StateHasChanged();
        });
    }

    private void RefreshState()
    {
        var activeErrors = ErrorService.GetActiveErrors();
        _resettableErrorsCount = activeErrors.Count(error => error.ActivatesResetButton);
        var shouldShowBadge = ShouldShowBadge;
        if (!_wasBadgeVisible && shouldShowBadge)
        {
            _pendingBlinkRestart = true;
            _blinkRestartAttemptCount = 0;
        }

        _wasBadgeVisible = shouldShowBadge;
        if (!shouldShowBadge)
        {
            _pendingBlinkRestart = false;
            _blinkRestartAttemptCount = 0;
        }

        if (_resettableErrorsCount == 0)
        {
            _isPanelOpen = false;
        }
    }

    private async Task RetryBlinkRestartAsync(Exception ex)
    {
        _blinkRestartAttemptCount++;
        if (_blinkRestartAttemptCount >= MaxBlinkRestartAttempts || !ShouldShowBadge || _disposed)
        {
            _pendingBlinkRestart = false;
            Logger.LogWarning(ex,
                "Не удалось перезапустить мигание floating error badge после {AttemptCount} попыток",
                _blinkRestartAttemptCount);
            return;
        }

        Logger.LogDebug(ex,
            "Повторный запуск мигания floating error badge: попытка {AttemptCount} из {MaxAttempts}",
            _blinkRestartAttemptCount,
            MaxBlinkRestartAttempts);

        await InvokeAsync(StateHasChanged);
    }

    private async Task TogglePanel()
    {
        if (!ShouldShowBadge)
        {
            return;
        }

        var hasRecentDrag = await JsRuntime.InvokeAsync<bool>("floatingPanel.consumeRecentDrag", BadgeElementId);
        if (hasRecentDrag)
        {
            return;
        }

        if (!_isPanelOpen)
        {
            await JsRuntime.InvokeVoidAsync("floatingPanel.resetToCenter", PanelElementId);
        }

        _isPanelOpen = !_isPanelOpen;
    }

    private void ClosePanel()
    {
        _isPanelOpen = false;
    }

    private void StartBadgeDrag(Microsoft.AspNetCore.Components.Web.MouseEventArgs e)
    {
        if (!ShouldShowBadge)
        {
            return;
        }
        _ = JsRuntime.InvokeVoidAsync("floatingPanel.startDrag", BadgeElementId, e.ClientX, e.ClientY);
    }

    private void StartPanelDrag(Microsoft.AspNetCore.Components.Web.MouseEventArgs e)
    {
        if (!ShouldShowPanel)
        {
            return;
        }
        _ = JsRuntime.InvokeVoidAsync("floatingPanel.startDrag", PanelElementId, e.ClientX, e.ClientY);
    }

    public ValueTask DisposeAsync()
    {
        _disposed = true;
        ErrorService.OnActiveErrorsChanged -= OnErrorsChanged;
        return ValueTask.CompletedTask;
    }
}

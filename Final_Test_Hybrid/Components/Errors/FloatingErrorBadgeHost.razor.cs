using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Services.Errors;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Final_Test_Hybrid.Components.Errors;

public partial class FloatingErrorBadgeHost : ComponentBase, IAsyncDisposable
{
    private const string BadgeElementId = "floating-error-badge";
    private const string PanelElementId = "floating-active-errors-window";

    [Inject] private IErrorService ErrorService { get; set; } = null!;
    [Inject] private AppSettingsService AppSettingsService { get; set; } = null!;
    [Inject] private IJSRuntime JsRuntime { get; set; } = null!;

    private bool _disposed;
    private bool _isPanelOpen;
    private int _resettableErrorsCount;

    private bool ShouldShowBadge => AppSettingsService.UseFloatingErrorBadge && _resettableErrorsCount > 0;
    private bool ShouldShowPanel => ShouldShowBadge && _isPanelOpen;

    protected override void OnInitialized()
    {
        ErrorService.OnActiveErrorsChanged += OnErrorsChanged;
        RefreshState();
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
        if (_resettableErrorsCount == 0)
        {
            _isPanelOpen = false;
        }
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

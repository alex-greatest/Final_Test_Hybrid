using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Completion;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Final_Test_Hybrid.Components.Main;

public partial class ResultImagePanel : ComponentBase, IAsyncDisposable
{
    [Inject] private TestCompletionUiState TestCompletionUiState { get; set; } = null!;
    [Inject] private RuntimeTerminalState RuntimeTerminalState { get; set; } = null!;
    [Inject] private IJSRuntime JsRuntime { get; set; } = null!;
    [Inject] private ILogger<ResultImagePanel> Logger { get; set; } = null!;

    private readonly string _imageElementId = $"result-image-{Guid.NewGuid():N}";
    private DotNetObjectReference<ResultImagePanel>? _dotNetReference;
    private int _attachedRenderVersion = -1;
    private int _lastFailedRenderVersion = -1;
    private bool _disposed;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed)
        {
            return;
        }

        var renderVersion = TestCompletionUiState.ImageRenderVersion;
        if (_attachedRenderVersion == renderVersion)
        {
            return;
        }

        await AttachProbeAsync(renderVersion);
    }

    [JSInvokable]
    public Task HandleImageLoaded(int renderVersion, int naturalWidth, int naturalHeight)
    {
        if (IsStaleRenderVersion(renderVersion))
        {
            return Task.CompletedTask;
        }

        if (naturalWidth <= 0 || naturalHeight <= 0)
        {
            LogImageFailure(renderVersion, "Картинка загружена, но имеет нулевой размер.");
        }

        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task HandleImageError(int renderVersion, string reason)
    {
        if (IsStaleRenderVersion(renderVersion))
        {
            return Task.CompletedTask;
        }

        LogImageFailure(renderVersion, reason);
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        await DetachProbeAsync();
        _dotNetReference?.Dispose();
    }

    private string GetImageRenderKey()
    {
        return $"{TestCompletionUiState.ImageRenderVersion}:{TestCompletionUiState.ImagePath}";
    }

    private async Task AttachProbeAsync(int renderVersion)
    {
        _dotNetReference ??= DotNetObjectReference.Create(this);
        _attachedRenderVersion = renderVersion;

        try
        {
            await JsRuntime.InvokeVoidAsync(
                "resultImageProbe.attach",
                _imageElementId,
                _dotNetReference,
                renderVersion);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            _attachedRenderVersion = -1;
            Logger.LogWarning(ex, "Не удалось подключить probe result-image для версии {RenderVersion}.", renderVersion);
        }
    }

    private async Task DetachProbeAsync()
    {
        if (_attachedRenderVersion < 0)
        {
            return;
        }

        try
        {
            await JsRuntime.InvokeVoidAsync("resultImageProbe.detach", _imageElementId);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            Logger.LogDebug(ex, "Не удалось отключить probe result-image {ElementId}.", _imageElementId);
        }

        _attachedRenderVersion = -1;
    }

    private bool IsStaleRenderVersion(int renderVersion)
    {
        return _disposed
            || !TestCompletionUiState.ShowResultImage
            || renderVersion != TestCompletionUiState.ImageRenderVersion;
    }

    private void LogImageFailure(int renderVersion, string reason)
    {
        if (_lastFailedRenderVersion == renderVersion)
        {
            return;
        }

        _lastFailedRenderVersion = renderVersion;
        Logger.LogWarning(
            "Не удалось отрисовать result-image: version={RenderVersion}, path={ImagePath}, result={TestResult}, show={ShowResultImage}, postAskEnd={IsPostAskEndActive}, reason={Reason}",
            renderVersion,
            TestCompletionUiState.ImagePath,
            TestCompletionUiState.TestResult,
            TestCompletionUiState.ShowResultImage,
            RuntimeTerminalState.IsPostAskEndActive,
            reason);
    }
}

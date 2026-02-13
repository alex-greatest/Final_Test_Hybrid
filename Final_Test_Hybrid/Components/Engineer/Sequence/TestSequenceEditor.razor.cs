using Final_Test_Hybrid.Models;
using Final_Test_Hybrid.Services.Common.IO;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Radzen.Blazor;

namespace Final_Test_Hybrid.Components.Engineer.Sequence;

public partial class TestSequenceEditor : IAsyncDisposable
{
    private const string GridContainerId = "test-sequence-editor-grid-container";

    [Inject]
    public required IFilePickerService FilePickerService { get; set; }
    [Inject]
    public required IJSRuntime JsRuntime { get; set; }
    [Inject]
    public required ITestStepRegistry StepRegistry { get; set; }
    private RadzenDataGrid<SequenceRow>? _grid;
    private List<SequenceRow> _rows = [];
    private IReadOnlyList<string> _stepNames = [];
    private HashSet<string> _visibleStepNames = [];
    private readonly CancellationTokenSource _cts = new();
    private readonly int _columnCount = 4;
    private bool _disposed;
    private bool _isLoading = true;
    private bool _isFileActive;
    private bool _shouldScrollGridToTop;

    protected override async Task OnInitializedAsync()
    {
        await Task.Yield();
        var stepNames = StepRegistry.VisibleSteps
            .Select(static step => step.Name)
            .ToList();
        _stepNames = stepNames;
        _visibleStepNames = stepNames.ToHashSet(StringComparer.Ordinal);
        _rows = TestSequenceService.InitializeRows(20, _columnCount);
        _isFileActive = !string.IsNullOrEmpty(TestSequenceService.CurrentFilePath);
        _isLoading = false;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed || !_shouldScrollGridToTop)
        {
            return;
        }

        _shouldScrollGridToTop = false;

        try
        {
            await JsRuntime.InvokeVoidAsync("scrollGridToTop", GridContainerId);
        }
        catch (JSDisconnectedException ex)
        {
            Logger.LogWarning(ex, "Скролл грида вверх пропущен: JS runtime отключен");
        }
        catch (JSException ex)
        {
            Logger.LogWarning(ex, "Скролл грида вверх не выполнен");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        await _cts.CancelAsync();
        _cts.Dispose();
        ResetServiceState();
    }

    private void ResetServiceState()
    {
        TestSequenceService.CurrentFilePath = null;
        TestSequenceService.CurrentFileName = null;
    }
}

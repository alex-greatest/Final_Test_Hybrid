using Final_Test_Hybrid.Models;
using Final_Test_Hybrid.Services.Common.IO;
using Final_Test_Hybrid.Services.Steps;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen.Blazor;

namespace Final_Test_Hybrid.Components.Engineer.Sequence;

public partial class TestSequenceEditor : IAsyncDisposable
{
    [Inject]
    public required IFilePickerService FilePickerService { get; set; }
    [Inject]
    public required IJSRuntime JsRuntime { get; set; }
    [Inject]
    public required ITestStepRegistry StepRegistry { get; set; }
    private RadzenDataGrid<SequenceRow>? _grid;
    private List<SequenceRow> _rows = [];
    private IReadOnlyList<ITestStep> _steps = [];
    private readonly CancellationTokenSource _cts = new();
    private readonly int _columnCount = 4;
    private bool _disposed;
    private bool _isLoading = true;
    private bool _isFileActive;

    protected override async Task OnInitializedAsync()
    {
        await Task.Yield();
        _steps = StepRegistry.VisibleSteps;
        _rows = TestSequenceService.InitializeRows(20, _columnCount);
        _isFileActive = !string.IsNullOrEmpty(TestSequenceService.CurrentFilePath);
        _isLoading = false;
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

using Radzen.Blazor;
using Final_Test_Hybrid.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Final_Test_Hybrid.Services.IO;

namespace Final_Test_Hybrid.Components.Engineer;

public partial class TestSequenceEditor : IDisposable
{
    [Inject]
    public required IFilePickerService FilePickerService { get; set; }
    [Inject]
    public required IJSRuntime JSRuntime { get; set; }

    private RadzenDataGrid<SequenceRow>? _grid;
    private List<SequenceRow> _rows = [];
    private readonly int _columnCount = 4;
    private bool _disposed;
    private bool _isLoading;
    private bool _isFileActive;

    protected override void OnInitialized()
    {
        _rows = TestSequenceService.InitializeRows(20, _columnCount);
        _isFileActive = !string.IsNullOrEmpty(TestSequenceService.CurrentFilePath);
    }

    public void Dispose()
    {
        _disposed = true;
    }
}

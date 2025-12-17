using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Services.Database;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Radzen;
using Radzen.Blazor;

namespace Final_Test_Hybrid.Components.Engineer.StandDatabase;

public partial class StepSelectDialog
{
    [Inject]
    public required StepFinalTestService StepFinalTestService { get; set; }
    [Inject]
    public required DialogService DialogService { get; set; }
    [Inject]
    public required ILogger<StepSelectDialog> Logger { get; set; }

    public static readonly object ClearStepMarker = new();

    private List<StepFinalTest> _steps = [];
    private IList<StepFinalTest> _selectedItems = [];
    private RadzenDataGrid<StepFinalTest>? _grid;
    private bool _loadError;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _steps = await StepFinalTestService.GetAllAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load steps");
            _loadError = true;
        }
    }

    private void OnRowDoubleClick(DataGridRowMouseEventArgs<StepFinalTest> args)
    {
        CloseWithResult(args.Data);
    }

    private void OnSelectClick()
    {
        var selectedStep = GetSelectedStep();
        CloseWithResult(selectedStep);
    }

    private void OnClearClick()
    {
        DialogService.Close(ClearStepMarker);
    }

    private void OnCancelClick()
    {
        DialogService.Close();
    }

    private StepFinalTest? GetSelectedStep()
    {
        return _selectedItems.FirstOrDefault();
    }

    private void CloseWithResult(StepFinalTest? step)
    {
        DialogService.Close(step);
    }
}

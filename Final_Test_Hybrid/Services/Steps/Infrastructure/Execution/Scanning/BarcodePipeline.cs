using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Main.Messages;
using Final_Test_Hybrid.Services.Scanner;
using Final_Test_Hybrid.Services.SpringBoot.Recipe;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Scanning;

/// <summary>
/// Fluent pipeline for barcode processing.
/// Each step can return a failure result to short-circuit execution,
/// or null to continue to the next step.
/// </summary>
public class BarcodePipeline(string barcode)
{
    public string Barcode { get; } = barcode;
    public BarcodeValidationResult Validation { get; set; } = null!;
    public BoilerTypeCycle Cycle { get; set; } = null!;
    public IReadOnlyList<RecipeResponseDto> Recipes { get; set; } = [];
    public List<string?[]> RawSequenceData { get; set; } = null!;
    public List<RawTestMap> RawMaps { get; set; } = null!;

    private readonly List<PipelineStep> _steps = [];

    /// <summary>
    /// Фаза для шага. Если null - сообщение не меняется.
    /// </summary>
    public ExecutionPhase? StepPhase { get; private set; }

    public BarcodePipeline Step(ExecutionPhase? phase, Func<BarcodePipeline, BarcodeStepResult?> action)
    {
        var asyncAction = WrapAsAsync(action);
        _steps.Add(new PipelineStep(phase, asyncAction));
        return this;
    }

    public BarcodePipeline StepAsync(ExecutionPhase? phase, Func<BarcodePipeline, Task<BarcodeStepResult?>> action)
    {
        _steps.Add(new PipelineStep(phase, action));
        return this;
    }

    public async Task<BarcodeStepResult> ExecuteAsync(ExecutionPhaseState phaseState)
    {
        foreach (var step in _steps)
        {
            var result = await ExecuteStepAsync(step, phaseState);
            if (result != null)
            {
                return result;
            }
        }
        return BarcodeStepResult.Pass([]);
    }

    private async Task<BarcodeStepResult?> ExecuteStepAsync(PipelineStep step, ExecutionPhaseState phaseState)
    {
        if (step.Phase.HasValue)
        {
            phaseState.SetPhase(step.Phase.Value);
        }
        return await step.Action(this);
    }

    private static Func<BarcodePipeline, Task<BarcodeStepResult?>> WrapAsAsync(
        Func<BarcodePipeline, BarcodeStepResult?> syncAction)
    {
        return pipeline => Task.FromResult(syncAction(pipeline));
    }

    private record PipelineStep(ExecutionPhase? Phase, Func<BarcodePipeline, Task<BarcodeStepResult?>> Action);
}

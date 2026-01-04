using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Main;
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

    public BarcodePipeline Step(string message, Func<BarcodePipeline, BarcodeStepResult?> action)
    {
        var asyncAction = WrapAsAsync(action);
        _steps.Add(new PipelineStep(message, asyncAction));
        return this;
    }

    public BarcodePipeline StepAsync(string message, Func<BarcodePipeline, Task<BarcodeStepResult?>> action)
    {
        _steps.Add(new PipelineStep(message, action));
        return this;
    }

    public async Task<BarcodeStepResult> ExecuteAsync(ExecutionMessageState messageState)
    {
        foreach (var step in _steps)
        {
            var result = await ExecuteStepAsync(step, messageState);
            if (result != null)
            {
                return result;
            }
        }
        return BarcodeStepResult.Pass([]);
    }

    private async Task<BarcodeStepResult?> ExecuteStepAsync(PipelineStep step, ExecutionMessageState messageState)
    {
        messageState.SetMessage(step.Message);
        return await step.Action(this);
    }

    private static Func<BarcodePipeline, Task<BarcodeStepResult?>> WrapAsAsync(
        Func<BarcodePipeline, BarcodeStepResult?> syncAction)
    {
        return pipeline => Task.FromResult(syncAction(pipeline));
    }

    private record PipelineStep(string Message, Func<BarcodePipeline, Task<BarcodeStepResult?>> Action);
}

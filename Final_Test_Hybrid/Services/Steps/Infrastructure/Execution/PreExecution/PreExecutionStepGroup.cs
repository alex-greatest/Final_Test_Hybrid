using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;

/// <summary>
/// Группа шагов PreExecution. Выполняет главный шаг и подшаги последовательно.
/// В гриде отображается как один шаг (главный).
/// </summary>
public class PreExecutionStepGroup(IPreExecutionStep mainStep, params IPreExecutionStep[] subSteps)
    : IPreExecutionStep
{
    private readonly IReadOnlyList<IPreExecutionStep> _subSteps = subSteps;

    public string Id => mainStep.Id;
    public string Name => mainStep.Name;
    public string Description => mainStep.Description;
    public bool IsVisibleInStatusGrid => true;

    public async Task<PreExecutionResult> ExecuteAsync(PreExecutionContext context, CancellationToken ct)
    {
        var result = await mainStep.ExecuteAsync(context, ct);
        if (result.Status != PreExecutionStatus.Continue)
        {
            return result;
        }
        return await ExecuteSubStepsAsync(context, result.SuccessMessage, ct);
    }

    private async Task<PreExecutionResult> ExecuteSubStepsAsync(
        PreExecutionContext context,
        string? successMessage,
        CancellationToken ct)
    {
        foreach (var subStep in _subSteps)
        {
            var result = await subStep.ExecuteAsync(context, ct);
            if (result.Status != PreExecutionStatus.Continue)
            {
                return result;
            }
        }
        return PreExecutionResult.Continue(successMessage);
    }
}

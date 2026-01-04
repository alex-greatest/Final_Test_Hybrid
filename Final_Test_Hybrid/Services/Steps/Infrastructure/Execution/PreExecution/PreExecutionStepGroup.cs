using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.PreExecution;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;

/// <summary>
/// Группа шагов PreExecution. Выполняет главный шаг и подшаги последовательно.
/// В гриде отображается как один шаг (главный).
/// </summary>
public class PreExecutionStepGroup : IPreExecutionStep
{
    private readonly IPreExecutionStep _mainStep;
    private readonly IReadOnlyList<IPreExecutionStep> _subSteps;

    public string Id => _mainStep.Id;
    public string Name => _mainStep.Name;
    public string Description => _mainStep.Description;
    public bool IsVisibleInStatusGrid => true;

    public PreExecutionStepGroup(IPreExecutionStep mainStep, params IPreExecutionStep[] subSteps)
    {
        _mainStep = mainStep;
        _subSteps = subSteps;
    }

    public async Task<PreExecutionResult> ExecuteAsync(PreExecutionContext context, CancellationToken ct)
    {
        var result = await _mainStep.ExecuteAsync(context, ct);
        if (!result.Success)
        {
            return result;
        }
        return await ExecuteSubStepsAsync(context, ct);
    }

    private async Task<PreExecutionResult> ExecuteSubStepsAsync(PreExecutionContext context, CancellationToken ct)
    {
        foreach (var subStep in _subSteps)
        {
            var result = await subStep.ExecuteAsync(context, ct);
            if (!result.Success)
            {
                return result;
            }
        }
        return PreExecutionResult.Ok();
    }
}

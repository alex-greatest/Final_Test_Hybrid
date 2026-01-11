using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;

namespace Final_Test_Hybrid.Services.Steps.Steps;

public class ResolveTestMapsStep(
    ITestMapResolver mapResolver,
    ExecutionMessageState messageState,
    DualLogger<ResolveTestMapsStep> logger) : IPreExecutionStep
{
    public string Id => "resolve-test-maps";
    public string Name => "Проверка шагов";
    public string Description => "Проверяет наличие шагов тестирования";
    public bool IsVisibleInStatusGrid => false;

    public Task<PreExecutionResult> ExecuteAsync(PreExecutionContext context, CancellationToken ct)
    {
        messageState.SetMessage("Проверка шагов...");
        var validationResult = ValidateRawMaps(context);
        return Task.FromResult(validationResult.Status == PreExecutionStatus.Failed ? validationResult : ResolveAndApplyMaps(context));
    }

    private PreExecutionResult ValidateRawMaps(PreExecutionContext context)
    {
        return !HasRawMaps(context) ? Fail("Нет тестовых последовательностей") : PreExecutionResult.Continue();
    }

    private PreExecutionResult ResolveAndApplyMaps(PreExecutionContext context)
    {
        var resolveResult = mapResolver.Resolve(context.RawMaps!);
        if (resolveResult.UnknownSteps.Count > 0)
        {
            return HandleUnknownSteps(resolveResult);
        }
        context.Maps = resolveResult.Maps;
        logger.LogInformation("Проверка шагов завершена: {Count} maps", context.Maps!.Count);
        return PreExecutionResult.Continue();
    }

    private static bool HasRawMaps(PreExecutionContext context)
    {
        return context.RawMaps is { Count: > 0 };
    }

    private PreExecutionResult HandleUnknownSteps(ResolveResult resolveResult)
    {
        var error = $"Неизвестных шагов: {resolveResult.UnknownSteps.Count}";
        logger.LogWarning("{Error}", error);
        return PreExecutionResult.Fail(error, new UnknownStepsDetails(resolveResult.UnknownSteps), "Ошибка проверки шагов");
    }

    private PreExecutionResult Fail(string error)
    {
        logger.LogWarning("{Error}", error);
        return PreExecutionResult.Fail(error, "Ошибка проверки шагов");
    }
}

using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;

namespace Final_Test_Hybrid.Services.Steps.Steps.PreExecution;

public class BuildTestMapsStep(
    ITestMapBuilder mapBuilder,
    DualLogger<BuildTestMapsStep> logger) : IPreExecutionStep
{
    public string Id => "build-test-maps";
    public string Name => "Построение карт тестов";
    public string Description => "Построение структуры тестовых карт";
    public bool IsVisibleInStatusGrid => false;

    public Task<PreExecutionResult> ExecuteAsync(PreExecutionContext context, CancellationToken ct)
    {
        var result = mapBuilder.Build(context.RawSequenceData!);
        if (!result.IsSuccess)
        {
            return Task.FromResult(PreExecutionResult.Fail(result.Error!));
        }
        context.RawMaps = result.Maps;
        logger.LogInformation("Построено карт тестов: {Count}", result.Maps!.Count);
        return Task.FromResult(PreExecutionResult.Continue());
    }
}

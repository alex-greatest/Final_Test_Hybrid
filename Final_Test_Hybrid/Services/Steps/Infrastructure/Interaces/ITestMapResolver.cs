using Final_Test_Hybrid.Models.Steps;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces;

public record ResolveResult(
    List<TestMap>? Maps,
    IReadOnlyList<UnknownStepInfo> UnknownSteps)
{
    public bool IsSuccess => Maps != null && UnknownSteps.Count == 0;

    public static ResolveResult Success(List<TestMap> maps) =>
        new(maps, []);

    public static ResolveResult WithUnknownSteps(List<TestMap> maps, IReadOnlyList<UnknownStepInfo> unknownSteps) =>
        new(maps, unknownSteps);
}

public interface ITestMapResolver
{
    ResolveResult Resolve(List<RawTestMap> rawMaps);
}

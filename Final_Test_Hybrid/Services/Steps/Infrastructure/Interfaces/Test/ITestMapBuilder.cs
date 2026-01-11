using Final_Test_Hybrid.Models.Steps;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;

public record RawMapBuildResult(
    List<RawTestMap>? Maps,
    string? Error)
{
    public bool IsSuccess => Error == null && Maps != null;

    public static RawMapBuildResult Success(List<RawTestMap> maps) =>
        new(maps, null);

    public static RawMapBuildResult WithError(string error) =>
        new(null, error);
}

public interface ITestMapBuilder
{
    RawMapBuildResult Build(List<string?[]> rawData);
}

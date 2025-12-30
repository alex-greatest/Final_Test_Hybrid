namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.Test;

public record SequenceLoadResult(List<string?[]>? RawData, string? Error)
{
    public bool IsSuccess => Error == null && RawData != null;

    public static SequenceLoadResult Success(List<string?[]> rawData) =>
        new(rawData, null);

    public static SequenceLoadResult WithError(string error) =>
        new(null, error);
}

public interface ITestSequenceLoader
{
    Task<SequenceLoadResult> LoadRawDataAsync(string articleNumber);
}

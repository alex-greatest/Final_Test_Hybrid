using System.Text.Json.Serialization;

namespace Final_Test_Hybrid.Services.SpringBoot.Operation.Finish;

/// <summary>
/// DTO для элемента результата теста с диапазоном (IsRanged=true).
/// </summary>
public class FinalTestResultItemLimited
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; init; } = string.Empty;

    [JsonPropertyName("min")]
    public string Min { get; init; } = string.Empty;

    [JsonPropertyName("max")]
    public string Max { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("test")]
    public string Test { get; init; } = string.Empty;

    [JsonPropertyName("valueType")]
    public string ValueType { get; init; } = "real";
}

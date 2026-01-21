using System.Text.Json.Serialization;

namespace Final_Test_Hybrid.Services.SpringBoot.Operation.Finish;

/// <summary>
/// DTO для времени выполнения шага теста.
/// </summary>
public class FinalTestResultTime
{
    [JsonPropertyName("test")]
    public string Test { get; init; } = string.Empty;

    [JsonPropertyName("time")]
    public string Time { get; init; } = string.Empty;
}

using Final_Test_Hybrid.Services.SpringBoot.Operation.Finish;
using System.Text.Json.Serialization;

namespace Final_Test_Hybrid.Services.SpringBoot.Operation.Interrupt;

/// <summary>
/// DTO для отправки информации о прерванной операции в MES.
/// </summary>
public class InterruptedOperationRequest
{
    [JsonPropertyName("serialNumber")]
    public string SerialNumber { get; init; } = string.Empty;

    [JsonPropertyName("stationName")]
    public string StationName { get; init; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("adminInterrupted")]
    public string AdminInterrupted { get; init; } = string.Empty;

    [JsonPropertyName("operator")]
    public string? Operator { get; init; }

    [JsonPropertyName("Items")]
    public List<FinalTestResultItem>? Items { get; init; }

    [JsonPropertyName("Items_limited")]
    public List<FinalTestResultItemLimited>? ItemsLimited { get; init; }

    [JsonPropertyName("time")]
    public List<FinalTestResultTime>? Time { get; init; }

    [JsonPropertyName("errors")]
    public List<string>? Errors { get; init; }

    [JsonPropertyName("result")]
    public int? Result { get; init; }
}

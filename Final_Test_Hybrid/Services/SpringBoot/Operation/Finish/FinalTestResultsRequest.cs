using System.Text.Json.Serialization;

namespace Final_Test_Hybrid.Services.SpringBoot.Operation.Finish;

/// <summary>
/// DTO запроса для завершения операции тестирования.
/// Отправляется на POST /api/operation/finish.
/// </summary>
public class FinalTestResultsRequest
{
    [JsonPropertyName("serialNumber")]
    public string SerialNumber { get; init; } = string.Empty;

    [JsonPropertyName("stationName")]
    public string StationName { get; init; } = string.Empty;

    [JsonPropertyName("operator")]
    public string Operator { get; init; } = string.Empty;

    [JsonPropertyName("Items")]
    public List<FinalTestResultItem> Items { get; init; } = [];

    [JsonPropertyName("Items_limited")]
    public List<FinalTestResultItemLimited> ItemsLimited { get; init; } = [];

    [JsonPropertyName("time")]
    public List<FinalTestResultTime> Time { get; init; } = [];

    [JsonPropertyName("errors")]
    public List<string> Errors { get; init; } = [];

    [JsonPropertyName("result")]
    public int Result { get; init; }
}

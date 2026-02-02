using System.Text.Json.Serialization;

namespace Final_Test_Hybrid.Services.SpringBoot.Operation.Interrupt;

/// <summary>
/// DTO для отправки информации о прерванной операции в MES.
/// </summary>
public record InterruptedOperationRequest(
    [property: JsonPropertyName("serialNumber")] string SerialNumber,
    [property: JsonPropertyName("stationName")] string StationName,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("adminInterrupted")] string AdminInterrupted);

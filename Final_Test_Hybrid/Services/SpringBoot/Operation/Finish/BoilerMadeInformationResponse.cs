using System.Text.Json.Serialization;

namespace Final_Test_Hybrid.Services.SpringBoot.Operation.Finish;

/// <summary>
/// DTO ответа на успешное завершение операции.
/// Возвращается при HTTP 200.
/// </summary>
public class BoilerMadeInformationResponse
{
    [JsonPropertyName("orderNumber")]
    public int OrderNumber { get; set; }

    [JsonPropertyName("amountBoilerOrder")]
    public int AmountBoilerOrder { get; set; }

    [JsonPropertyName("amountBoilerMadeOrder")]
    public int AmountBoilerMadeOrder { get; set; }
}

using System.Text.Json.Serialization;

namespace Final_Test_Hybrid.Services.SpringBoot.Operation;

public class OperationStartResponse
{
    public BoilerMadeInformation BoilerMadeInformation { get; set; } = new();
    public BoilerTypeCycleDto BoilerTypeCycle { get; set; } = new();
    public List<RecipeDto> Recipes { get; set; } = [];
}

public class BoilerMadeInformation
{
    public int OrderNumber { get; set; }
    public int AmountBoilerMadeOrder { get; set; }
    public int AmountBoilerOrder { get; set; }
    public string? CorrelationId { get; set; }
}

public class BoilerTypeCycleDto
{
    public string TypeName { get; set; } = string.Empty;
    public string Article { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
}

public class RecipeDto
{
    public string Parameter { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PlcTypeDto PlcType { get; set; }
}

public enum PlcTypeDto
{
    STRING,
    INT,
    REAL,
    BOOL
}

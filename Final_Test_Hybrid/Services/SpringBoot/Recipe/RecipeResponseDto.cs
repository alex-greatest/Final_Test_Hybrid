using System.Text.Json.Serialization;
using Final_Test_Hybrid.Models.Database;

namespace Final_Test_Hybrid.Services.SpringBoot.Recipe;

public class RecipeResponseDto
{
    private string _address = string.Empty;
    private bool? _isPlc;

    [JsonPropertyName("tagName")]
    public string TagName { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string Address
    {
        get => !string.IsNullOrEmpty(_address) ? _address : GetAddressFromTagName();
        set => _address = value;
    }

    public PlcType PlcType { get; set; }

    public bool IsPlc
    {
        get => _isPlc ?? TagName.StartsWith("ns=");
        set => _isPlc = value;
    }

    public string? Unit { get; set; }
    public string? Description { get; set; }

    private string GetAddressFromTagName()
    {
        return TagName.StartsWith("ns=") ? TagName : string.Empty;
    }
}

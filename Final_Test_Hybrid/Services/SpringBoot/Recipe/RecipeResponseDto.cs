using Final_Test_Hybrid.Models.Database;

namespace Final_Test_Hybrid.Services.SpringBoot.Recipe;

public class RecipeResponseDto
{
    public string TagName { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public PlcType PlcType { get; set; }
    public bool IsPlc { get; set; }
    public string? Unit { get; set; }
    public string? Description { get; set; }
}

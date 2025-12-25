using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Services.Scanner;
using Final_Test_Hybrid.Services.SpringBoot.Recipe;

namespace Final_Test_Hybrid.Models.Steps;

internal sealed class BarcodeContext(string barcode)
{
    public string Barcode { get; } = barcode;
    public BarcodeValidationResult Validation { get; set; } = null!;
    public BoilerTypeCycle Cycle { get; set; } = null!;
    public IReadOnlyList<RecipeResponseDto> Recipes { get; set; } = null!;
}

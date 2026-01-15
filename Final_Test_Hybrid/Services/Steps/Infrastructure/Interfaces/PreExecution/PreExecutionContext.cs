using Final_Test_Hybrid.Models;
using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.Scanner;
using Final_Test_Hybrid.Services.SpringBoot.Recipe;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;

public class PreExecutionContext
{
    public required string Barcode { get; init; }
    public required BoilerState BoilerState { get; init; }
    public required PausableOpcUaTagService OpcUa { get; init; }
    public required ITestStepLogger TestStepLogger { get; init; }

    public Guid? ScanStepId { get; set; }
    public List<RawTestMap>? RawMaps { get; set; }
    public List<TestMap>? Maps { get; set; }

    // Новые поля для пайплайна
    public BarcodeValidationResult? BarcodeValidation { get; set; }
    public BoilerTypeCycle? BoilerTypeCycle { get; set; }
    public IReadOnlyList<RecipeResponseDto>? Recipes { get; set; }
    public List<string?[]>? RawSequenceData { get; set; }
}

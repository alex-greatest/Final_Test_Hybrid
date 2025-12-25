using Final_Test_Hybrid.Services.Steps.Infrastructure;

namespace Final_Test_Hybrid.Services.Steps.Interaces;

public interface IScanBarcodeStep
{
    Task<StepResult> ProcessBarcodeAsync(string barcode);
    IReadOnlyList<string> LastMissingTags { get; }
}

using Final_Test_Hybrid.Services.Steps.Infrastructure;

namespace Final_Test_Hybrid.Services.Steps.Interaces;

public interface IScanBarcodeStep
{
    Task<BarcodeStepResult> ProcessBarcodeAsync(string barcode);
}

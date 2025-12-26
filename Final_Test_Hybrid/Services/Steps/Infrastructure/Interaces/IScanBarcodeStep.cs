using Final_Test_Hybrid.Models.Steps;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces;

public interface IScanBarcodeStep
{
    Task<BarcodeStepResult> ProcessBarcodeAsync(string barcode);
}

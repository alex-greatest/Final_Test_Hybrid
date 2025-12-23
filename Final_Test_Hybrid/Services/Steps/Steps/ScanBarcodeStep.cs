namespace Final_Test_Hybrid.Services.Steps.Steps;

public class ScanBarcodeStep : ITestStep
{
    public string Id => "scan-barcode";
    public string Name => "Сканирование штрихкода";
    public string Description => "Сканирует штрихкод с продукта";
    public bool IsVisibleInEditor => false;

    public Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        return Task.FromResult(TestStepResult.Pass());
    }
}

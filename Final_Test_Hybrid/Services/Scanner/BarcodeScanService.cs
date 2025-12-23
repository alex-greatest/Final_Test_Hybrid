using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Steps.Execution;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Scanner;

public class BarcodeScanService(
    BoilerState boilerState,
    SequenceValidationState validationState,
    ILogger<BarcodeScanService> logger)
{
    private const int MinBarcodeLength = 11;
    private const int ArticleLength = 10;
    public event Action<string>? OnScanSuccess;

    public void ProcessBarcode(string barcode)
    {
        logger.LogInformation("Обработка штрихкода: {Barcode}", barcode);
        validationState.ClearError();
        if (!IsValidLength(barcode))
        {
            HandleInvalidBarcode(barcode);
            return;
        }
        HandleValidBarcode(barcode);
    }

    private bool IsValidLength(string barcode)
    {
        return barcode.Length >= MinBarcodeLength;
    }

    private void HandleInvalidBarcode(string barcode)
    {
        logger.LogWarning("Штрихкод слишком короткий: {Barcode}", barcode);
        boilerState.SetData(barcode, article: "", isValid: false);
        validationState.SetError("Штрихкод слишком короткий");
    }

    private void HandleValidBarcode(string barcode)
    {
        var article = barcode[^ArticleLength..];
        logger.LogInformation("Успешное сканирование. Серийный номер: {Serial}, Артикул: {Article}", barcode, article);
        boilerState.SetData(barcode, article, isValid: true);
        OnScanSuccess?.Invoke(article);
    }
}

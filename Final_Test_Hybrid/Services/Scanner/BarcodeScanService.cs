using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Services.Database;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Steps.Execution;
using Final_Test_Hybrid.Services.Steps.Manage;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Scanner;

public class BarcodeScanService(
    BoilerState boilerState,
    SequenceValidationState validationState,
    BoilerTypeService boilerTypeService,
    TestSequenseService testSequenseService,
    ILogger<BarcodeScanService> logger)
{
    private const int MinBarcodeLength = 11;
    private const int ArticleLength = 10;
    public event Action<string>? OnScanSuccess;

    public async Task ProcessBarcodeAsync(string barcode)
    {
        logger.LogInformation("Обработка штрихкода: {Barcode}", barcode);
        validationState.ClearError();
        if (!IsValidLength(barcode))
        {
            HandleInvalidBarcode(barcode);
            return;
        }
        await HandleValidBarcodeAsync(barcode);
    }

    private bool IsValidLength(string barcode)
    {
        return barcode.Length >= MinBarcodeLength;
    }

    private void HandleInvalidBarcode(string barcode)
    {
        logger.LogWarning("Штрихкод слишком короткий: {Barcode}", barcode);
        boilerState.SetData(barcode, article: "", isValid: false);
        SetValidationError("Штрихкод слишком короткий");
    }

    private void SetValidationError(string message)
    {
        validationState.SetError(message);
        testSequenseService.SetErrorOnCurrent(message);
    }

    private async Task HandleValidBarcodeAsync(string barcode)
    {
        var article = barcode[^ArticleLength..];
        var boilerTypeCycle = await FindActiveBoilerTypeCycleAsync(article);
        if (boilerTypeCycle == null)
        {
            boilerState.SetData(barcode, article, isValid: false);
            return;
        }
        logger.LogInformation("Успешное сканирование. Серийный номер: {Serial}, Артикул: {Article}, Тип: {Type}",
            barcode, article, boilerTypeCycle.Type);
        boilerState.SetData(barcode, article, isValid: true, boilerTypeCycle);
        OnScanSuccess?.Invoke(article);
    }

    private async Task<BoilerTypeCycle?> FindActiveBoilerTypeCycleAsync(string article)
    {
        try
        {
            var boilerTypeCycle = await boilerTypeService.FindActiveByArticleAsync(article);
            if (boilerTypeCycle != null)
            {
                return boilerTypeCycle;
            }
            logger.LogWarning("Тип котла не найден: {Article}", article);
            SetValidationError("Тип котла не найден");
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка БД при поиске типа котла: {Article}", article);
            SetValidationError("Ошибка БД");
            return null;
        }
    }
}

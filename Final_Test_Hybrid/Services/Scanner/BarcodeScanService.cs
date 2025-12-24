namespace Final_Test_Hybrid.Services.Scanner;

public class BarcodeScanService
{
    private const int MinBarcodeLength = 11;
    private const int ArticleLength = 10;

    public BarcodeValidationResult Validate(string barcode)
    {
        if (barcode.Length < MinBarcodeLength)
        {
            return BarcodeValidationResult.Invalid("Штрихкод слишком короткий");
        }
        var article = barcode[^ArticleLength..];
        return BarcodeValidationResult.Valid(barcode, article);
    }
}

public record BarcodeValidationResult(bool IsValid, string Barcode, string? Article, string? Error)
{
    public static BarcodeValidationResult Valid(string barcode, string article)
        => new(true, barcode, article, null);

    public static BarcodeValidationResult Invalid(string error)
        => new(false, "", null, error);
}

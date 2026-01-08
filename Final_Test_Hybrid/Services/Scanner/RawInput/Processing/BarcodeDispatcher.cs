using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Scanner.RawInput.Processing;

/// <summary>
/// Dispatches completed barcodes to session handlers or fallback event.
/// </summary>
public sealed class BarcodeDispatcher(ScanSessionHandler sessionHandler, ILogger logger)
{
    private Action<string>? _fallbackHandler;

    public void SetFallbackHandler(Action<string>? handler) => _fallbackHandler = handler;

    public void Dispatch(string barcode)
    {
        try
        {
            DispatchToTarget(barcode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in barcode handler");
        }
    }

    private void DispatchToTarget(string barcode)
    {
        if (TryDispatchToSession(barcode))
        {
            return;
        }
        DispatchToFallback(barcode);
    }

    private bool TryDispatchToSession(string barcode)
    {
        var handler = sessionHandler.GetHandler();
        if (handler == null)
        {
            return false;
        }
        handler(barcode);
        return true;
    }

    private void DispatchToFallback(string barcode) => _fallbackHandler?.Invoke(barcode);
}

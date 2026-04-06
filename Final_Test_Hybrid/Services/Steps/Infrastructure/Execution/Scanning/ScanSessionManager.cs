using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Scanning;

public class ScanSessionManager(
    ScannerInputOwnershipService scannerOwnership,
    ILogger<ScanSessionManager> logger)
    : IDisposable
{
    private readonly Lock _sessionLock = new();
    private Action<string>? _barcodeHandler;

    public void AcquireSession(Action<string> barcodeHandler)
    {
        lock (_sessionLock)
        {
            if (CanReuseExistingSessionUnsafe(barcodeHandler))
            {
                return;
            }

            _barcodeHandler = barcodeHandler;
            scannerOwnership.EnsurePreExecutionOwner(barcodeHandler);
            logger.LogDebug("Scan session acquired");
        }
    }

    private bool CanReuseExistingSessionUnsafe(Action<string> barcodeHandler)
    {
        // PLC reset clears ownership directly in ScannerInputOwnershipService,
        // so the cached handler alone is not enough to treat the session as alive.
        return _barcodeHandler != null
               && ReferenceEquals(_barcodeHandler, barcodeHandler)
               && scannerOwnership.GetCurrentOwnerState().HasPreExecutionOwner;
    }

    public void ReleaseSession()
    {
        lock (_sessionLock)
        {
            if (_barcodeHandler == null)
            {
                return;
            }
            _barcodeHandler = null;
            scannerOwnership.ReleasePreExecutionOwner();
            logger.LogDebug("Scan session released");
        }
    }

    public void Dispose()
    {
        ReleaseSession();
    }
}

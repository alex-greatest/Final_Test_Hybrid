using Final_Test_Hybrid.Services.Scanner.RawInput;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Scanning;

public class ScanSessionManager(
    RawInputService rawInputService,
    ILogger<ScanSessionManager> logger)
    : IDisposable
{
    private readonly Lock _sessionLock = new();
    private IDisposable? _scanSession;
    public bool HasActiveSession => _scanSession != null;

    public void AcquireSession(Action<string> barcodeHandler)
    {
        lock (_sessionLock)
        {
            if (_scanSession != null)
            {
                return;
            }
            _scanSession = rawInputService.RequestScan(barcodeHandler, takeOver: false);
            logger.LogDebug("Scan session acquired");
        }
    }

    public void ReleaseSession()
    {
        lock (_sessionLock)
        {
            if (_scanSession == null)
            {
                return;
            }
            _scanSession.Dispose();
            _scanSession = null;
            logger.LogDebug("Scan session released");
        }
    }

    public void Dispose()
    {
        ReleaseSession();
    }
}

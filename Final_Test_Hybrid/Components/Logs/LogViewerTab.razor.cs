using System.Diagnostics;
using Final_Test_Hybrid.Services.Common.Logging;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Components.Logs;

public partial class LogViewerTab : IDisposable
{
    private const int MaxLines = 10_000;
    private const int RefreshIntervalMs = 3000;
    private const int SlowRefreshThresholdMs = 500;

    private readonly LogFileTailReader _reader = new(MaxLines);
    private System.Threading.Timer? _refreshTimer;
    private List<string> _lines = [];
    private string? _statusMessage;
    private int _selectedTabIndex;
    private int _refreshInProgress;
    private bool _disposed;
    private bool _isActive;

    [Inject]
    private ITestStepLogger TestStepLogger { get; set; } = null!;

    [Inject]
    private ILogger<LogViewerTab> Logger { get; set; } = null!;

    protected override async Task OnInitializedAsync()
    {
        await RefreshLogAsync(allowInactive: true);
    }

    private void StartAutoRefresh()
    {
        _refreshTimer ??= new System.Threading.Timer(
            HandleRefreshTimerTick,
            null,
            Timeout.Infinite,
            Timeout.Infinite);

        _refreshTimer.Change(RefreshIntervalMs, RefreshIntervalMs);
    }

    private void StopAutoRefresh()
    {
        _refreshTimer?.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private void HandleRefreshTimerTick(object? timerState)
    {
        _ = timerState;
        if (_disposed || !_isActive)
        {
            return;
        }

        try
        {
            _ = InvokeAsync(HandleTimerTickAsync);
        }
        catch (InvalidOperationException) when (_disposed)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task HandleTimerTickAsync()
    {
        if (_disposed || !_isActive)
        {
            return;
        }

        await RefreshLogAsync();
    }

    public async Task SetActiveAsync(bool isActive)
    {
        if (_disposed || _isActive == isActive)
        {
            return;
        }

        _isActive = isActive;
        if (_isActive)
        {
            StartAutoRefresh();
            await RefreshLogAsync(allowInactive: true);
            return;
        }

        StopAutoRefresh();
    }

    private async Task RefreshLogAsync(bool allowInactive = false)
    {
        if (_disposed || (!_isActive && !allowInactive) || Interlocked.Exchange(ref _refreshInProgress, 1) == 1)
        {
            return;
        }

        try
        {
            var snapshot = await ReadSnapshotAsync();
            if (_disposed)
            {
                return;
            }

            _lines = snapshot.Lines.ToList();
            _statusMessage = snapshot.Message;
            await RequestRenderAsync();
        }
        catch (Exception ex)
        {
            if (_disposed)
            {
                return;
            }

            _lines = [];
            _statusMessage = "Ошибка чтения файла лога";
            Logger.LogError(ex, "LogViewerTab не смог прочитать лог-файл");
            await RequestRenderAsync();
        }
        finally
        {
            Interlocked.Exchange(ref _refreshInProgress, 0);
        }
    }

    private async Task<LogTailSnapshot> ReadSnapshotAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        var path = TestStepLogger.GetCurrentLogFilePath();
        var snapshot = await Task.Run(() => _reader.Refresh(path));
        stopwatch.Stop();
        if (Logger.IsEnabled(LogLevel.Warning)
            && stopwatch.ElapsedMilliseconds > SlowRefreshThresholdMs)
        {
            Logger.LogWarning(
                "Обновление LogViewerTab заняло {DurationMs} мс. Строк={LineCount}",
                stopwatch.ElapsedMilliseconds,
                snapshot.Lines.Count);
        }

        return snapshot;
    }

    private async Task RequestRenderAsync()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            await InvokeAsync(StateHasChanged);
        }
        catch (InvalidOperationException) when (_disposed)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static string GetLogLevelClass(string line)
    {
        if (line.Contains("[ERR]"))
        {
            return "log-error";
        }

        if (line.Contains("[WRN]"))
        {
            return "log-warning";
        }

        return line.Contains("[DBG]") ? "log-debug" : "";
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopAutoRefresh();
        _refreshTimer?.Dispose();
        GC.SuppressFinalize(this);
    }
}

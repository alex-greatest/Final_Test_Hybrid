using AsyncAwaitBestPractices;
using Final_Test_Hybrid.Models.Plc.Subcription;
using Final_Test_Hybrid.Models.Plc.Tags;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.OpcUa.Connection;

namespace Final_Test_Hybrid.Services.OpcUa.Auto;

public sealed class PlcAutoWriterService : IDisposable
{
    private const int MaxWriteAttempts = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(300);
    private readonly OpcUaConnectionState _connectionState;
    private readonly OpcUaTagService _tagService;
    private readonly DualLogger<PlcAutoWriterService> _logger;
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private bool _disposed;

    public PlcAutoWriterService(
        OpcUaConnectionState connectionState,
        OpcUaTagService tagService,
        DualLogger<PlcAutoWriterService> logger)
    {
        _connectionState = connectionState;
        _tagService = tagService;
        _logger = logger;

        _connectionState.ConnectionStateChanged += HandleConnectionStateChanged;
        if (_connectionState.IsConnected)
        {
            TriggerWriteAutoTrue();
        }
    }

    private void HandleConnectionStateChanged(bool isConnected)
    {
        if (!isConnected || _disposed)
        {
            return;
        }

        TriggerWriteAutoTrue();
    }

    private void TriggerWriteAutoTrue()
    {
        WriteAutoTrueOnConnectedSafeAsync(_disposeCts.Token).SafeFireAndForget(ex =>
            _logger.LogError(ex, "Ошибка автозаписи PLC тега Test.Auto"));
    }

    private async Task WriteAutoTrueOnConnectedSafeAsync(CancellationToken ct)
    {
        await _writeGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await WriteAutoTrueWithRetryAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private async Task WriteAutoTrueWithRetryAsync(CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MaxWriteAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            await _connectionState.WaitForConnectionAsync(ct).ConfigureAwait(false);

            var result = await _tagService.WriteAsync(BaseTags.TestAuto, true, ct, silent: true)
                .ConfigureAwait(false);
            if (result.Success)
            {
                _logger.LogInformation(
                    "PLC тег {Tag} записан: true (попытка {Attempt}/{MaxAttempts})",
                    BaseTags.TestAuto,
                    attempt,
                    MaxWriteAttempts);
                return;
            }

            if (!ShouldRetry(result, attempt))
            {
                _logger.LogWarning(
                    "Не удалось записать PLC тег {Tag} = true: {Error}",
                    BaseTags.TestAuto,
                    result.Error ?? "неизвестная ошибка");
                return;
            }

            _logger.LogWarning(
                "Не удалось записать PLC тег {Tag} = true. Попытка {Attempt}/{MaxAttempts}. Ошибка: {Error}",
                BaseTags.TestAuto,
                attempt,
                MaxWriteAttempts,
                result.Error ?? "неизвестная ошибка");
            await Task.Delay(RetryDelay, ct).ConfigureAwait(false);
        }

        _logger.LogWarning(
            "PLC тег {Tag} = true не записан после {MaxAttempts} попыток",
            BaseTags.TestAuto,
            MaxWriteAttempts);
    }

    private static bool ShouldRetry(WriteResult result, int attempt)
    {
        return attempt < MaxWriteAttempts && IsTransientWriteError(result.Error);
    }

    private static bool IsTransientWriteError(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return false;
        }

        return error.Contains("BadNotConnected", StringComparison.OrdinalIgnoreCase)
            || error.Contains("BadSessionClosed", StringComparison.OrdinalIgnoreCase)
            || error.Contains("BadSessionIdInvalid", StringComparison.OrdinalIgnoreCase)
            || error.Contains("BadSessionNotActivated", StringComparison.OrdinalIgnoreCase)
            || error.Contains("BadConnectionClosed", StringComparison.OrdinalIgnoreCase)
            || error.Contains("BadServerNotConnected", StringComparison.OrdinalIgnoreCase)
            || error.Contains("BadSecureChannelClosed", StringComparison.OrdinalIgnoreCase)
            || error.Contains("BadCommunicationError", StringComparison.OrdinalIgnoreCase)
            || error.Contains("Соединение закрыто", StringComparison.OrdinalIgnoreCase)
            || error.Contains("Сервер не подключён", StringComparison.OrdinalIgnoreCase)
            || error.Contains("connection closed", StringComparison.OrdinalIgnoreCase)
            || error.Contains("not connected", StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _connectionState.ConnectionStateChanged -= HandleConnectionStateChanged;
        _disposeCts.Cancel();
        _disposeCts.Dispose();
    }
}

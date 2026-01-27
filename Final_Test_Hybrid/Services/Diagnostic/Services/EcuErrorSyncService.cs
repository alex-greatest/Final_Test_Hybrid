using Final_Test_Hybrid.Models.Errors;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Models;
using Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;
using Final_Test_Hybrid.Services.Errors;

namespace Final_Test_Hybrid.Services.Diagnostic.Services;

/// <summary>
/// Синхронизирует ошибки ЭБУ с ErrorService на основе данных ping.
/// </summary>
public sealed class EcuErrorSyncService : IDisposable
{
    private readonly IModbusDispatcher _dispatcher;
    private readonly IErrorService _errorService;
    private readonly DualLogger<EcuErrorSyncService> _logger;
    private readonly Lock _lock = new();
    private ushort _currentErrorId;
    private bool _disposed;

    /// <summary>
    /// Создаёт сервис синхронизации ошибок ЭБУ.
    /// </summary>
    /// <param name="dispatcher">Диспетчер Modbus для подписки на события.</param>
    /// <param name="errorService">Сервис ошибок для взведения/сброса.</param>
    /// <param name="logger">Логгер.</param>
    public EcuErrorSyncService(
        IModbusDispatcher dispatcher,
        IErrorService errorService,
        DualLogger<EcuErrorSyncService> logger)
    {
        _dispatcher = dispatcher;
        _errorService = errorService;
        _logger = logger;
        _dispatcher.PingDataUpdated += OnPingDataUpdated;
        _dispatcher.Disconnecting += OnDisconnecting;
    }

    /// <summary>
    /// Обработчик обновления данных ping.
    /// </summary>
    private void OnPingDataUpdated(DiagnosticPingData data)
    {
        if (data.LastErrorId is null)
        {
            return;
        }

        var newErrorId = data.LastErrorId.Value;

        lock (_lock)
        {
            if (_disposed || newErrorId == _currentErrorId)
            {
                return;
            }

            ClearCurrentErrorInternal();

            if (newErrorId > 0)
            {
                var newError = ErrorDefinitions.GetEcuErrorById(newErrorId);
                if (newError != null)
                {
                    _logger.LogWarning("Ошибка ЭБУ: {Code} - {Description}", newError.Code, newError.Description);
                    _errorService.Raise(newError);
                }
                else
                {
                    _logger.LogWarning("Неизвестная ошибка ЭБУ: ID={ErrorId}", newErrorId);
                }
            }

            _currentErrorId = newErrorId;
        }
    }

    /// <summary>
    /// Очищает ошибку ЭБУ и сбрасывает внутреннее состояние при отключении.
    /// </summary>
    private Task OnDisconnecting()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return Task.CompletedTask;
            }

            ClearCurrentErrorInternal();
            _currentErrorId = 0;
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Очищает текущую ошибку если она установлена. Вызывать только под lock.
    /// </summary>
    private void ClearCurrentErrorInternal()
    {
        if (_currentErrorId > 0)
        {
            var error = ErrorDefinitions.GetEcuErrorById(_currentErrorId);
            if (error != null)
            {
                _logger.LogInformation("Сброс ошибки ЭБУ: {Code}", error.Code);
                _errorService.Clear(error.Code);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            ClearCurrentErrorInternal();
            _currentErrorId = 0;
        }

        // Отписка вне lock — безопасно и избегает потенциального deadlock
        _dispatcher.PingDataUpdated -= OnPingDataUpdated;
        _dispatcher.Disconnecting -= OnDisconnecting;
    }
}

using Final_Test_Hybrid.Models.Errors;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Models;
using Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;
using Final_Test_Hybrid.Services.Errors;

namespace Final_Test_Hybrid.Services.Diagnostic.Services;

/// <summary>
/// Синхронизирует ошибки ЭБУ с ErrorService на основе данных ping.
/// Взводит ошибку только в lock-контексте (status 1/2 + whitelist 111.txt).
/// </summary>
public sealed class EcuErrorSyncService : IDisposable
{
    private const ushort ErrorIdE9 = 1;
    private const int TemperatureThreshold = 100;

    private readonly IModbusDispatcher _dispatcher;
    private readonly IErrorService _errorService;
    private readonly DualLogger<EcuErrorSyncService> _logger;
    private readonly Lock _lock = new();
    private ushort _currentErrorId;
    private ErrorDefinition? _currentError;
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
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            if (!BoilerLockCriteria.IsLockStatus(data.BoilerStatus))
            {
                ClearErrorWhenLockContextGone(data.BoilerStatus);
                return;
            }

            if (data.LastErrorId is null)
            {
                return;
            }

            SyncErrorInLockContext(data.LastErrorId.Value, data.ChTemperature);
        }
    }

    /// <summary>
    /// Синхронизирует ошибку в lock-контексте.
    /// </summary>
    private void SyncErrorInLockContext(ushort errorId, short? chTemperature)
    {
        if (!BoilerLockCriteria.IsTargetErrorId(errorId))
        {
            ClearErrorWhenNotInWhitelist(errorId);
            return;
        }

        var newError = ResolveEcuError(errorId, chTemperature);
        if (errorId == _currentErrorId && AreErrorsEqual(_currentError, newError))
        {
            return;
        }

        ClearCurrentErrorInternal();

        if (newError != null)
        {
            _logger.LogWarning("Ошибка ЭБУ (lock): {Code} - {Description}", newError.Code, newError.Description);
            _errorService.Raise(newError);
        }
        else
        {
            _logger.LogWarning("Неизвестная lock-ошибка ЭБУ: ID={ErrorId}", errorId);
        }

        _currentErrorId = errorId;
        _currentError = newError;
    }

    /// <summary>
    /// Очищает текущую ECU-ошибку при выходе из lock-контекста.
    /// </summary>
    private void ClearErrorWhenLockContextGone(short boilerStatus)
    {
        if (_currentError is null && _currentErrorId == 0)
        {
            return;
        }

        _logger.LogInformation("ECU lock-контекст завершён (BoilerStatus={Status}). Снимаем активную ECU-ошибку.", boilerStatus);
        ClearCurrentErrorInternal();
        _currentErrorId = 0;
        _currentError = null;
    }

    /// <summary>
    /// Очищает текущую ECU-ошибку если код из 1047 не относится к lock-whitelist.
    /// </summary>
    private void ClearErrorWhenNotInWhitelist(ushort errorId)
    {
        if (_currentError != null)
        {
            _logger.LogInformation("Сброс ECU-ошибки: код 1047={ErrorId} не входит в lock-whitelist.", errorId);
            ClearCurrentErrorInternal();
        }

        _currentErrorId = errorId;
        _currentError = null;
    }

    /// <summary>
    /// Определяет ErrorDefinition с учётом E9 + температура.
    /// При null температуре сохраняет предыдущую классификацию E9 (защита от флаппинга).
    /// </summary>
    private ErrorDefinition? ResolveEcuError(ushort errorId, short? chTemperature)
    {
        if (errorId == ErrorIdE9)
        {
            switch (chTemperature)
            {
                // Если температура не прочиталась — сохраняем предыдущую классификацию
                // Если текущая ошибка — вариант E9, оставить её
                case null when _currentError == ErrorDefinitions.EcuE9 || _currentError == ErrorDefinitions.EcuE9Stb:
                    return _currentError;
                // Иначе fallback на обычную E9
                case null:
                    return ErrorDefinitions.EcuE9;
                // Температура известна — классифицируем
                case < TemperatureThreshold:
                    return ErrorDefinitions.EcuE9Stb;
            }
        }
        return ErrorDefinitions.GetEcuErrorById(errorId);
    }

    /// <summary>
    /// Сравнивает две ошибки по коду.
    /// </summary>
    private static bool AreErrorsEqual(ErrorDefinition? a, ErrorDefinition? b)
    {
        if (a is null && b is null)
        {
            return true;
        }
        if (a is null || b is null)
        {
            return false;
        }
        return a.Code == b.Code;
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
            ResetStateInternal();
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Очищает текущую ошибку если она установлена. Вызывать только под lock.
    /// </summary>
    private void ClearCurrentErrorInternal()
    {
        if (_currentError != null)
        {
            _logger.LogInformation("Сброс ошибки ЭБУ: {Code}", _currentError.Code);
            _errorService.Clear(_currentError.Code);
        }
    }

    /// <summary>
    /// Сбрасывает внутреннее состояние синхронизации. Вызывать только под lock.
    /// </summary>
    private void ResetStateInternal()
    {
        _currentErrorId = 0;
        _currentError = null;
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
            ResetStateInternal();
        }

        // Отписка вне lock — безопасно и избегает потенциального deadlock
        _dispatcher.PingDataUpdated -= OnPingDataUpdated;
        _dispatcher.Disconnecting -= OnDisconnecting;
    }
}

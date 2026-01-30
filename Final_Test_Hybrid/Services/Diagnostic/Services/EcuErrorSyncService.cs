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
        if (data.LastErrorId is null)
        {
            return;
        }

        var newErrorId = data.LastErrorId.Value;

        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            // Дедупликация по ID для неизвестных ошибок
            if (newErrorId == _currentErrorId && newErrorId > 0)
            {
                // Для E9 проверяем не изменилась ли классификация
                if (newErrorId != ErrorIdE9)
                {
                    return; // Не E9 — точно дубликат
                }
                // E9 — проверяем классификацию ниже
            }
            else if (newErrorId == _currentErrorId)
            {
                return; // Оба 0 или оба неизвестные — дубликат
            }

            var newError = newErrorId > 0 ? ResolveEcuError(newErrorId, data.ChTemperature) : null;

            // Для E9 проверяем изменилась ли классификация
            if (newErrorId == ErrorIdE9 && _currentErrorId == ErrorIdE9 && AreErrorsEqual(_currentError, newError))
            {
                return; // E9 та же классификация — дубликат
            }

            ClearCurrentErrorInternal();

            if (newError != null)
            {
                _logger.LogWarning("Ошибка ЭБУ: {Code} - {Description}", newError.Code, newError.Description);
                _errorService.Raise(newError);
            }
            else if (newErrorId > 0)
            {
                _logger.LogWarning("Неизвестная ошибка ЭБУ: ID={ErrorId}", newErrorId);
            }

            _currentErrorId = newErrorId;
            _currentError = newError;
        }
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
            _currentErrorId = 0;
            _currentError = null;
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
            _currentError = null;
        }

        // Отписка вне lock — безопасно и избегает потенциального deadlock
        _dispatcher.PingDataUpdated -= OnPingDataUpdated;
        _dispatcher.Disconnecting -= OnDisconnecting;
    }
}

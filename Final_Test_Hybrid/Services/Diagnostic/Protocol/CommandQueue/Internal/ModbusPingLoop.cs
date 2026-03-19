using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Diagnostic.Models;

namespace Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue.Internal;

/// <summary>
/// Генератор периодических ping команд.
/// Это генератор команд, не сервис связи.
/// Использует enqueueFunc для отправки команд в очередь.
/// </summary>
internal sealed class ModbusPingLoop
{
    private readonly Func<IModbusCommand, CancellationToken, ValueTask> _enqueueFunc;
    private readonly ModbusDispatcherOptions _options;
    private readonly ushort _baseAddressOffset;
    private readonly ExecutionActivityTracker _activityTracker;

    /// <summary>
    /// Вызывается при получении данных ping (только если не останавливаемся).
    /// </summary>
    public Action<DiagnosticPingData>? OnPingDataReceived;

    /// <summary>
    /// Создаёт экземпляр генератора ping.
    /// </summary>
    /// <param name="enqueueFunc">Функция для отправки команд в очередь.</param>
    /// <param name="options">Настройки диспетчера.</param>
    /// <param name="baseAddressOffset">Смещение базового адреса из DiagnosticSettings.</param>
    /// <param name="activityTracker">Трекер активности execution для выбора ping-профиля.</param>
    public ModbusPingLoop(
        Func<IModbusCommand, CancellationToken, ValueTask> enqueueFunc,
        ModbusDispatcherOptions options,
        ushort baseAddressOffset,
        ExecutionActivityTracker activityTracker)
    {
        _enqueueFunc = enqueueFunc;
        _options = options;
        _baseAddressOffset = baseAddressOffset;
        _activityTracker = activityTracker;
    }

    /// <summary>
    /// Запускает цикл ping. Проверяет isPortOpen перед отправкой.
    /// </summary>
    /// <param name="isPortOpen">Функция проверки открытия порта.</param>
    /// <param name="isStopping">Функция проверки остановки (для избежания race при StopAsync).</param>
    /// <param name="ct">Токен отмены.</param>
    public async Task RunAsync(Func<bool> isPortOpen, Func<bool> isStopping, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var interval = GetCurrentInterval();
                await Task.Delay(interval, ct).ConfigureAwait(false);

                // Только отправляем ping если порт открыт и не останавливаемся
                if (!isPortOpen() || isStopping())
                {
                    continue;
                }

                try
                {
                    var command = new PingCommand(CommandPriority.Low, _baseAddressOffset, ct);
                    await _enqueueFunc(command, ct).ConfigureAwait(false);

                    var pingData = await command.Task.WaitAsync(ct).ConfigureAwait(false);

                    // Проверяем порт и остановку перед обновлением данных (race protection)
                    // isPortOpen защищает от in-flight ping при разрыве соединения
                    if (isPortOpen() && !isStopping())
                    {
                        OnPingDataReceived?.Invoke(pingData);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception)
                {
                    // Ping failure обрабатывается dispatcher'ом - он залогирует ошибку связи
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected при StopAsync
        }
    }

    private TimeSpan GetCurrentInterval()
    {
        var intervalMs = _activityTracker.IsTestExecutionActive
            ? _options.PingIntervalActiveMs
            : _options.PingIntervalIdleMs;

        return TimeSpan.FromMilliseconds(intervalMs);
    }
}

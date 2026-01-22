using Final_Test_Hybrid.Services.Common.Logging;
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
    private readonly IDualLogger _logger;

    /// <summary>
    /// Вызывается при получении данных ping (только если не останавливаемся).
    /// </summary>
    public Action<DiagnosticPingData>? OnPingDataReceived;

    /// <summary>
    /// Создаёт экземпляр генератора ping.
    /// </summary>
    /// <param name="enqueueFunc">Функция для отправки команд в очередь.</param>
    /// <param name="options">Настройки диспетчера.</param>
    /// <param name="logger">Логгер.</param>
    public ModbusPingLoop(
        Func<IModbusCommand, CancellationToken, ValueTask> enqueueFunc,
        ModbusDispatcherOptions options,
        IDualLogger logger)
    {
        _enqueueFunc = enqueueFunc;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Запускает цикл ping. Проверяет isPortOpen перед отправкой.
    /// </summary>
    /// <param name="isPortOpen">Функция проверки открытия порта.</param>
    /// <param name="isStopping">Функция проверки остановки (для избежания race при StopAsync).</param>
    /// <param name="ct">Токен отмены.</param>
    public async Task RunAsync(Func<bool> isPortOpen, Func<bool> isStopping, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_options.PingIntervalMs));

        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                // Только отправляем ping если порт открыт и не останавливаемся
                if (!isPortOpen() || isStopping())
                {
                    continue;
                }

                try
                {
                    var command = new PingCommand(CommandPriority.Low, ct);
                    await _enqueueFunc(command, ct).ConfigureAwait(false);

                    var pingData = await command.Task.WaitAsync(ct).ConfigureAwait(false);

                    // Проверяем порт и остановку перед обновлением данных (race protection)
                    // isPortOpen защищает от in-flight ping при разрыве соединения
                    if (isPortOpen() && !isStopping())
                    {
                        OnPingDataReceived?.Invoke(pingData);

                        _logger.LogDebug("Ping OK: ModeKey={ModeKey:X8}, BoilerStatus={BoilerStatus}",
                            pingData.ModeKey, pingData.BoilerStatus);
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
}

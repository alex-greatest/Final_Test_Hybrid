using System.Threading.Channels;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Coordinator;

public partial class TestExecutionCoordinator
{
    private Channel<bool>? _errorSignalChannel;

    /// <summary>
    /// Создаёт и запускает канал для сигналов об ошибках.
    /// </summary>
    private Channel<bool> StartErrorSignalChannel()
    {
        var channel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropWrite
        });
        Interlocked.Exchange(ref _errorSignalChannel, channel);
        return channel;
    }

    /// <summary>
    /// Сигнализирует об обнаружении ошибки.
    /// </summary>
    private void SignalErrorDetected()
    {
        var channel = Volatile.Read(ref _errorSignalChannel);
        channel?.Writer.TryWrite(true);
    }

    /// <summary>
    /// Завершает канал сигналов после окончания выполнения.
    /// </summary>
    private void CompleteErrorSignalChannel()
    {
        var channel = Interlocked.Exchange(ref _errorSignalChannel, null);
        if (channel == null)
        {
            return;
        }
        channel.Writer.TryWrite(true);
        channel.Writer.TryComplete();
    }

    /// <summary>
    /// Запускает цикл обработки ошибок.
    /// </summary>
    private Task RunErrorHandlingLoopAsync(ChannelReader<bool> reader, CancellationToken token)
    {
        return ProcessErrorSignalsAsync(reader, token);
    }

    /// <summary>
    /// Обрабатывает сигналы об ошибках из канала.
    /// </summary>
    private async Task ProcessErrorSignalsAsync(ChannelReader<bool> reader, CancellationToken token)
    {
        try
        {
            await foreach (var _ in reader.ReadAllAsync(token))
            {
                await HandleErrorsIfAny();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}


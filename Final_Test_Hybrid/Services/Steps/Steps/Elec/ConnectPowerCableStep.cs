using Final_Test_Hybrid.Models.Errors;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Errors;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.Elec;

/// <summary>
/// Тестовый шаг подключения силового кабеля.
/// </summary>
public class ConnectPowerCableStep(
    DualLogger<ConnectPowerCableStep> logger,
    IErrorService errorService) : IHasPlcBlockPath, IRequiresPlcSubscriptions
{
    private const string BlockPath = "DB_VI.Elec.Connect_Power_Cable";
    private const string StartTag = "ns=3;s=\"DB_VI\".\"Elec\".\"Connect_Power_Cable\".\"Start\"";
    private const string EndTag = "ns=3;s=\"DB_VI\".\"Elec\".\"Connect_Power_Cable\".\"End\"";
    private const string ErrorTag = "ns=3;s=\"DB_VI\".\"Elec\".\"Connect_Power_Cable\".\"Error\"";

    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(30);

    public string Id => "elec-connect-power-cable";
    public string Name => "Elec/Connect_Power_Cable";
    public string Description => "Подключение силового кабеля.";
    public string PlcBlockPath => BlockPath;
    public IReadOnlyList<string> RequiredPlcTags => [StartTag, EndTag, ErrorTag];

    /// <summary>
    /// Выполняет шаг подключения силового кабеля.
    /// </summary>
    /// <param name="context">Контекст выполнения шага.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Запуск шага подключения силового кабеля");

        var writeResult = await context.OpcUa.WriteAsync(StartTag, true, ct);
        if (writeResult.Error != null)
        {
            return TestStepResult.Fail($"Ошибка записи Start: {writeResult.Error}");
        }

        return await WaitForCompletionWithTimeoutAsync(context, ct);
    }

    /// <summary>
    /// Ожидание End/Error с таймаутом для индикации ошибки.
    /// После записи Start запускаем таймер 30 сек и ждём End/Error бесконечно.
    /// Если за 30 сек ничего — показываем ошибку PowerCableNotConnected, но продолжаем ждать.
    /// </summary>
    private async Task<TestStepResult> WaitForCompletionWithTimeoutAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Запуск таймера {Timeout} сек для ожидания подключения кабеля", ReadyTimeout.TotalSeconds);

        using var timerCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var waitTask = context.TagWaiter.WaitAnyAsync(
            context.TagWaiter.CreateWaitGroup<WaitResult>()
                .WaitForTrue(EndTag, () => WaitResult.End, "End")
                .WaitForTrue(ErrorTag, () => WaitResult.Error, "Error"),
            ct);

        var timeoutTask = Task.Delay(ReadyTimeout, timerCts.Token);

        var errorRaised = false;

        var completedTask = await Task.WhenAny(waitTask, timeoutTask);

        // Таймаут сработал первым И успешно завершился (не отменён)
        if (completedTask == timeoutTask && timeoutTask.Status == TaskStatus.RanToCompletion)
        {
            errorRaised = true;
            errorService.RaiseInStep(ErrorDefinitions.PowerCableNotConnected, Id, Name);
            logger.LogWarning("Таймаут {Timeout} сек — силовой кабель не подключен", ReadyTimeout.TotalSeconds);
        }
        else if (completedTask == waitTask)
        {
            // End/Error пришёл первым — отменяем таймер
            await timerCts.CancelAsync();
        }

        try
        {
            // Дожидаемся End/Error в любом случае (пробросит OperationCanceledException если ct отменён)
            var result = await waitTask;

            return result.Result switch
            {
                WaitResult.End => await HandleSuccessAsync(context, ct),
                WaitResult.Error => TestStepResult.Fail(""),
                _ => TestStepResult.Fail("Неизвестный результат")
            };
        }
        finally
        {
            // Снимаем ошибку если была поднята (гарантированно, даже при исключении)
            if (errorRaised)
            {
                errorService.Clear(ErrorDefinitions.PowerCableNotConnected.Code);
            }
        }
    }

    /// <summary>
    /// Обрабатывает успешное завершение.
    /// </summary>
    private async Task<TestStepResult> HandleSuccessAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Шаг подключения силового кабеля завершён успешно");

        var writeResult = await context.OpcUa.WriteAsync(StartTag, false, ct);
        return writeResult.Error != null
            ? TestStepResult.Fail($"Ошибка сброса Start: {writeResult.Error}")
            : TestStepResult.Pass();
    }

    private enum WaitResult { End, Error }
}

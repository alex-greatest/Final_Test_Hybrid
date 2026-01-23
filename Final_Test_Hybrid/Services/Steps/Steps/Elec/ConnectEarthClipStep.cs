using Final_Test_Hybrid.Models.Errors;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Errors;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.Elec;

/// <summary>
/// Тестовый шаг проверки подключения клипсы заземления.
/// </summary>
public class ConnectEarthClipStep(
    DualLogger<ConnectEarthClipStep> logger,
    IErrorService errorService) : ITestStep, IHasPlcBlockPath, IRequiresPlcSubscriptions
{
    private const string BlockPath = "DB_VI.Elec.Connect_Earth_Clip";
    private const string StartTag = "ns=3;s=\"DB_VI\".\"Elec\".\"Connect_Earth_Clip\".\"Start\"";
    private const string EndTag = "ns=3;s=\"DB_VI\".\"Elec\".\"Connect_Earth_Clip\".\"End\"";
    private const string ErrorTag = "ns=3;s=\"DB_VI\".\"Elec\".\"Connect_Earth_Clip\".\"Error\"";
    private const string Ready1Tag = "ns=3;s=\"DB_VI\".\"Elec\".\"Connect_Earth_Clip\".\"Ready_1\"";

    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(30);

    public string Id => "elec-connect-earth-clip";
    public string Name => "Elec/Connect_Earth_Clip";
    public string Description => "Проверка заземления. Продувка в прямом направлении.";
    public string PlcBlockPath => BlockPath;
    public IReadOnlyList<string> RequiredPlcTags => [StartTag, EndTag, ErrorTag, Ready1Tag];

    /// <summary>
    /// Выполняет шаг проверки подключения клипсы заземления.
    /// </summary>
    /// <param name="context">Контекст выполнения шага.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Запуск проверки подключения клипсы заземления");

        var writeResult = await context.OpcUa.WriteAsync(StartTag, true, ct);
        if (writeResult.Error != null)
        {
            return TestStepResult.Fail($"Ошибка записи Start: {writeResult.Error}");
        }

        return await WaitPhase1Async(context, ct);
    }

    /// <summary>
    /// Фаза 1: Ожидание End/Error/Ready_1.
    /// </summary>
    private async Task<TestStepResult> WaitPhase1Async(TestStepContext context, CancellationToken ct)
    {
        var result = await context.TagWaiter.WaitAnyAsync(
            context.TagWaiter.CreateWaitGroup<Phase1Result>()
                .WaitForTrue(EndTag, () => Phase1Result.End, "End")
                .WaitForTrue(ErrorTag, () => Phase1Result.Error, "Error")
                .WaitForTrue(Ready1Tag, () => Phase1Result.Ready1, "Ready_1"),
            ct);

        return result.Result switch
        {
            Phase1Result.End => await HandleSuccessAsync(context, ct),
            Phase1Result.Error => TestStepResult.Fail(""),
            Phase1Result.Ready1 => await WaitPhase2WithTimeoutAsync(context, ct),
            _ => TestStepResult.Fail("Неизвестный результат")
        };
    }

    /// <summary>
    /// Фаза 2: После Ready_1 запускаем таймер 30 сек и ждём End/Error.
    /// Использует Task.WhenAny для избежания race condition между таймером и ожиданием тегов.
    /// </summary>
    private async Task<TestStepResult> WaitPhase2WithTimeoutAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Ready_1 получен, запуск таймера {Timeout} сек", ReadyTimeout.TotalSeconds);

        using var timerCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var waitTask = context.TagWaiter.WaitAnyAsync(
            context.TagWaiter.CreateWaitGroup<Phase2Result>()
                .WaitForTrue(EndTag, () => Phase2Result.End, "End")
                .WaitForTrue(ErrorTag, () => Phase2Result.Error, "Error"),
            ct);

        var timeoutTask = Task.Delay(ReadyTimeout, timerCts.Token);

        var errorRaised = false;

        var completedTask = await Task.WhenAny(waitTask, timeoutTask);

        // Таймаут сработал первым И успешно завершился (не отменён)
        if (completedTask == timeoutTask && timeoutTask.Status == TaskStatus.RanToCompletion)
        {
            errorRaised = true;
            errorService.RaiseInStep(ErrorDefinitions.EarthClipNotConnected, Id, Name);
            logger.LogWarning("Таймаут {Timeout} сек — клипса заземление не подключена", ReadyTimeout.TotalSeconds);
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
                Phase2Result.End => await HandleSuccessAsync(context, ct),
                Phase2Result.Error => TestStepResult.Fail(""),
                _ => TestStepResult.Fail("Неизвестный результат")
            };
        }
        finally
        {
            // Снимаем ошибку если была поднята (гарантированно, даже при исключении)
            if (errorRaised)
            {
                errorService.Clear(ErrorDefinitions.EarthClipNotConnected.Code);
            }
        }
    }

    /// <summary>
    /// Обрабатывает успешное завершение.
    /// </summary>
    private async Task<TestStepResult> HandleSuccessAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Проверка подключения клипсы заземления завершена успешно");

        var writeResult = await context.OpcUa.WriteAsync(StartTag, false, ct);
        return writeResult.Error != null
            ? TestStepResult.Fail($"Ошибка сброса Start: {writeResult.Error}")
            : TestStepResult.Pass();
    }

    private enum Phase1Result { End, Error, Ready1 }
    private enum Phase2Result { End, Error }
}

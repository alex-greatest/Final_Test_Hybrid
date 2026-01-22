using System.Collections.Concurrent;
using System.Diagnostics;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Polling;
using Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.Diagnostic;

/// <summary>
/// Уничтожительный стресс-тест для проверки PollingService на прочность:
/// race conditions, утечки ресурсов, поведение при разрыве связи.
/// </summary>
public class DiagPollingStressStep(
    PollingService pollingService,
    IModbusDispatcher dispatcher,
    DualLogger<DiagPollingStressStep> logger) : ITestStep
{
    private const int TaskCount = 20;
    private const int RapidCycles = 100;
    private const int ChurnIterations = 50;
    private const int DisconnectSimulations = 10;
    private const int MinIntervalMs = 50;
    private const int MaxIntervalMs = 500;
    private const int DeadlockTimeoutMs = 5000;
    private const int StopAllTimeoutMs = 2000;
    private const int MaxMemoryGrowthKb = 10240;
    private static readonly ushort[] TestAddresses = [0x1000, 0x1001];

    public string Id => "diag-polling-stress";
    public string Name => "DiagPollingStress";
    public string Description => "Уничтожительный стресс-тест polling";

    /// <summary>
    /// Выполняет стресс-тест PollingService.
    /// </summary>
    /// <param name="context">Контекст выполнения шага.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        var metrics = new StressTestMetrics();
        var totalSw = Stopwatch.StartNew();

        logger.LogInformation("▶ Старт стресс-теста PollingService");

        try
        {
            await RunPhase1ParallelTaskCreationAsync(metrics, ct);
            await RunPhase2RapidStartStopAsync(metrics, ct);
            await RunPhase3CreateRemoveChurnAsync(metrics, ct);
            await RunPhase4DisconnectSimulationAsync(metrics, ct);
            await RunPhase5StopAllTasksUnderLoadAsync(metrics, ct);
            await RunPhase6CleanupVerificationAsync(metrics);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            metrics.UnhandledExceptions++;
            logger.LogError(ex, "Необработанное исключение в стресс-тесте: {Error}", ex.Message);
        }

        totalSw.Stop();
        metrics.TotalDurationMs = totalSw.ElapsedMilliseconds;

        return EvaluateResults(metrics);
    }

    #region Phase 1: Parallel Task Creation

    /// <summary>
    /// Фаза 1: параллельное создание 20 задач с разными интервалами.
    /// </summary>
    private async Task RunPhase1ParallelTaskCreationAsync(StressTestMetrics metrics, CancellationToken ct)
    {
        logger.LogInformation("═══ Фаза 1: Parallel Task Creation ═══");
        var random = new Random();

        var creationTasks = Enumerable.Range(0, TaskCount)
            .Select(i => Task.Run(() => CreateTaskSafe(i, random.Next(MinIntervalMs, MaxIntervalMs), metrics), ct))
            .ToList();

        await Task.WhenAll(creationTasks);

        var startTasks = Enumerable.Range(0, TaskCount)
            .Select(i => Task.Run(() => StartTaskSafe($"stress-task-{i}"), ct))
            .ToList();

        await Task.WhenAll(startTasks);

        var activeTasks = pollingService.GetActiveTasks().Count;
        logger.LogInformation("Создано и запущено {Count} задач, активных: {Active}", TaskCount, activeTasks);

        if (activeTasks != TaskCount)
        {
            logger.LogWarning("Ожидалось {Expected} активных задач, получено {Actual}", TaskCount, activeTasks);
        }

        await StopAllCreatedTasksAsync(ct);
    }

    /// <summary>
    /// Создаёт polling-задачу с защитой от исключений.
    /// </summary>
    private void CreateTaskSafe(int index, int intervalMs, StressTestMetrics metrics)
    {
        try
        {
            pollingService.CreateTask(
                $"stress-task-{index}",
                TestAddresses,
                TimeSpan.FromMilliseconds(intervalMs),
                _ =>
                {
                    Interlocked.Increment(ref metrics.CallbackInvocations);
                    return Task.CompletedTask;
                });

            Interlocked.Increment(ref metrics.TasksCreated);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Ошибка при создании задачи {Index}: {Error}", index, ex.Message);
        }
    }

    /// <summary>
    /// Запускает задачу по имени с защитой от исключений.
    /// </summary>
    private void StartTaskSafe(string name)
    {
        try
        {
            pollingService.StartTask(name);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Ошибка при запуске задачи {Name}: {Error}", name, ex.Message);
        }
    }

    /// <summary>
    /// Останавливает все созданные тестовые задачи.
    /// </summary>
    private async Task StopAllCreatedTasksAsync(CancellationToken ct)
    {
        var stopTasks = Enumerable.Range(0, TaskCount)
            .Select(i => pollingService.StopTaskAsync($"stress-task-{i}"))
            .ToList();

        await Task.WhenAll(stopTasks);
        ct.ThrowIfCancellationRequested();
    }

    #endregion

    #region Phase 2: Rapid Start/Stop Race

    /// <summary>
    /// Фаза 2: быстрые циклы Start/Stop для детектирования race conditions.
    /// </summary>
    private async Task RunPhase2RapidStartStopAsync(StressTestMetrics metrics, CancellationToken ct)
    {
        logger.LogInformation("═══ Фаза 2: Rapid Start/Stop Race ═══");

        var raceTasks = Enumerable.Range(0, TaskCount)
            .Select(i => ExecuteRapidStartStopForTaskAsync(i, metrics, ct))
            .ToList();

        await Task.WhenAll(raceTasks);

        logger.LogInformation("Выполнено {Cycles} циклов Start/Stop", metrics.StartStopCycles);
    }

    /// <summary>
    /// Выполняет быстрые циклы Start/Stop для одной задачи.
    /// </summary>
    private async Task ExecuteRapidStartStopForTaskAsync(int taskIndex, StressTestMetrics metrics, CancellationToken ct)
    {
        var taskName = $"stress-task-{taskIndex}";

        for (var cycle = 0; cycle < RapidCycles; cycle++)
        {
            ct.ThrowIfCancellationRequested();

            var deadlockDetected = await ExecuteSingleStartStopCycleAsync(taskName, metrics);

            if (deadlockDetected)
            {
                logger.LogError("Deadlock обнаружен для задачи {Name} на цикле {Cycle}", taskName, cycle);
                break;
            }
        }
    }

    /// <summary>
    /// Выполняет один цикл Start/Stop с детекцией deadlock.
    /// </summary>
    private async Task<bool> ExecuteSingleStartStopCycleAsync(string taskName, StressTestMetrics metrics)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            pollingService.StartTask(taskName);

            var stopTask = pollingService.StopTaskAsync(taskName);
            var completedTask = await Task.WhenAny(stopTask, Task.Delay(DeadlockTimeoutMs));

            if (completedTask != stopTask)
            {
                Interlocked.Increment(ref metrics.DeadlocksDetected);
                return true;
            }

            sw.Stop();
            UpdateMaxStopTime(metrics, sw.ElapsedMilliseconds);
            Interlocked.Increment(ref metrics.StartStopCycles);
        }
        catch (Exception ex)
        {
            logger.LogDebug("Ошибка в цикле Start/Stop: {Error}", ex.Message);
        }

        return false;
    }

    /// <summary>
    /// Обновляет максимальное время остановки.
    /// </summary>
    private static void UpdateMaxStopTime(StressTestMetrics metrics, long elapsed)
    {
        long currentMax;
        do
        {
            currentMax = Interlocked.Read(ref metrics.MaxStopTimeMs);
            if (elapsed <= currentMax)
            {
                break;
            }
        } while (Interlocked.CompareExchange(ref metrics.MaxStopTimeMs, elapsed, currentMax) != currentMax);
    }

    #endregion

    #region Phase 3: Create/Remove Churn

    /// <summary>
    /// Фаза 3: быстрое создание-удаление задач для проверки утечек памяти.
    /// </summary>
    private async Task RunPhase3CreateRemoveChurnAsync(StressTestMetrics metrics, CancellationToken ct)
    {
        logger.LogInformation("═══ Фаза 3: Create/Remove Churn ═══");

        await CleanupExistingTasksAsync();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var memoryBefore = GC.GetTotalMemory(false);

        for (var i = 0; i < ChurnIterations; i++)
        {
            ct.ThrowIfCancellationRequested();
            await ExecuteSingleChurnIterationAsync(i, metrics);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var memoryAfter = GC.GetTotalMemory(false);

        metrics.MemoryGrowthKb = (memoryAfter - memoryBefore) / 1024;

        var finalTaskCount = pollingService.GetAllTasks().Count;
        logger.LogInformation(
            "Churn завершён. Memory growth: {Growth}KB, Final tasks: {Count}",
            metrics.MemoryGrowthKb, finalTaskCount);
    }

    /// <summary>
    /// Очищает все существующие тестовые задачи.
    /// </summary>
    private async Task CleanupExistingTasksAsync()
    {
        for (var i = 0; i < TaskCount; i++)
        {
            await pollingService.RemoveTaskAsync($"stress-task-{i}");
        }
    }

    /// <summary>
    /// Выполняет одну итерацию create-remove.
    /// </summary>
    private async Task ExecuteSingleChurnIterationAsync(int iteration, StressTestMetrics metrics)
    {
        var taskName = $"churn-task-{iteration}";

        try
        {
            pollingService.CreateTask(
                taskName,
                TestAddresses,
                TimeSpan.FromMilliseconds(100),
                _ => Task.CompletedTask);

            Interlocked.Increment(ref metrics.TasksCreated);

            pollingService.StartTask(taskName);
            await pollingService.StopTaskAsync(taskName);
            await pollingService.RemoveTaskAsync(taskName);
        }
        catch (Exception ex)
        {
            logger.LogDebug("Ошибка в churn итерации {Iteration}: {Error}", iteration, ex.Message);
        }
    }

    #endregion

    #region Phase 4: Disconnect Simulation

    /// <summary>
    /// Фаза 4: симуляция разрыва связи.
    /// </summary>
    private async Task RunPhase4DisconnectSimulationAsync(StressTestMetrics metrics, CancellationToken ct)
    {
        logger.LogInformation("═══ Фаза 4: Disconnect Simulation ═══");

        for (var sim = 0; sim < DisconnectSimulations; sim++)
        {
            ct.ThrowIfCancellationRequested();
            await ExecuteSingleDisconnectSimulationAsync(sim, metrics, ct);
            Interlocked.Increment(ref metrics.DisconnectSimulationCount);
        }

        logger.LogInformation("Выполнено {Count} симуляций разрыва связи", metrics.DisconnectSimulationCount);
    }

    /// <summary>
    /// Выполняет одну симуляцию разрыва связи.
    /// </summary>
    private async Task ExecuteSingleDisconnectSimulationAsync(int simIndex, StressTestMetrics metrics, CancellationToken ct)
    {
        const int tasksPerSimulation = 5;
        var taskNames = new List<string>();

        try
        {
            taskNames = await CreateAndStartSimulationTasksAsync(simIndex, tasksPerSimulation, metrics);

            await Task.Delay(200, ct);

            await SimulateDisconnectAsync();

            await VerifyAllTasksStoppedAsync(taskNames);

            await SimulateReconnectAsync(ct);
        }
        finally
        {
            await CleanupSimulationTasksAsync(taskNames);
        }
    }

    /// <summary>
    /// Создаёт и запускает задачи для симуляции.
    /// </summary>
    private async Task<List<string>> CreateAndStartSimulationTasksAsync(
        int simIndex,
        int count,
        StressTestMetrics metrics)
    {
        var taskNames = new List<string>();

        for (var i = 0; i < count; i++)
        {
            var taskName = $"disconnect-sim-{simIndex}-task-{i}";
            taskNames.Add(taskName);

            pollingService.CreateTask(
                taskName,
                TestAddresses,
                TimeSpan.FromMilliseconds(100),
                _ =>
                {
                    Interlocked.Increment(ref metrics.CallbackInvocations);
                    return Task.CompletedTask;
                });

            Interlocked.Increment(ref metrics.TasksCreated);
            pollingService.StartTask(taskName);
        }

        await Task.Yield();
        return taskNames;
    }

    /// <summary>
    /// Симулирует разрыв связи через остановку диспетчера.
    /// </summary>
    private async Task SimulateDisconnectAsync()
    {
        if (dispatcher.IsStarted)
        {
            await dispatcher.StopAsync();
        }
    }

    /// <summary>
    /// Проверяет что все задачи остановлены после разрыва связи.
    /// </summary>
    private async Task VerifyAllTasksStoppedAsync(List<string> taskNames)
    {
        await Task.Delay(500);

        var allTasks = pollingService.GetAllTasks();
        var runningCount = allTasks.Count(t => taskNames.Contains(t.Name) && t.IsRunning);

        if (runningCount > 0)
        {
            logger.LogWarning("После разрыва связи {Count} задач всё ещё работают", runningCount);
        }
    }

    /// <summary>
    /// Симулирует восстановление связи.
    /// </summary>
    private async Task SimulateReconnectAsync(CancellationToken ct)
    {
        if (!dispatcher.IsStarted)
        {
            await dispatcher.StartAsync(ct);
        }
    }

    /// <summary>
    /// Очищает задачи симуляции.
    /// </summary>
    private async Task CleanupSimulationTasksAsync(List<string> taskNames)
    {
        foreach (var name in taskNames)
        {
            try
            {
                await pollingService.StopTaskAsync(name);
                await pollingService.RemoveTaskAsync(name);
            }
            catch
            {
                // Игнорируем ошибки при очистке
            }
        }
    }

    #endregion

    #region Phase 5: StopAllTasks Under Load

    /// <summary>
    /// Фаза 5: тест остановки всех задач под нагрузкой.
    /// </summary>
    private async Task RunPhase5StopAllTasksUnderLoadAsync(StressTestMetrics metrics, CancellationToken ct)
    {
        logger.LogInformation("═══ Фаза 5: StopAllTasks Under Load ═══");

        var callbackCounts = new ConcurrentDictionary<string, int>();

        await CreateLoadTestTasksAsync(metrics, callbackCounts);

        await WaitForCallbacksAsync(callbackCounts, ct);

        var sw = Stopwatch.StartNew();
        var stopTask = pollingService.StopAllTasksAsync();
        var completedTask = await Task.WhenAny(stopTask, Task.Delay(StopAllTimeoutMs + 3000));
        sw.Stop();

        if (completedTask != stopTask)
        {
            logger.LogError("StopAllTasksAsync превысил таймаут {Timeout}ms", StopAllTimeoutMs);
            Interlocked.Increment(ref metrics.DeadlocksDetected);
        }
        else
        {
            logger.LogInformation("StopAllTasksAsync выполнен за {Elapsed}ms", sw.ElapsedMilliseconds);

            if (sw.ElapsedMilliseconds > StopAllTimeoutMs)
            {
                logger.LogWarning("StopAllTasksAsync медленнее ожидаемого: {Elapsed}ms > {Expected}ms",
                    sw.ElapsedMilliseconds, StopAllTimeoutMs);
            }
        }

        UpdateMaxStopTime(metrics, sw.ElapsedMilliseconds);
    }

    /// <summary>
    /// Создаёт задачи для нагрузочного теста.
    /// </summary>
    private async Task CreateLoadTestTasksAsync(
        StressTestMetrics metrics,
        ConcurrentDictionary<string, int> callbackCounts)
    {
        for (var i = 0; i < TaskCount; i++)
        {
            var taskName = $"load-task-{i}";
            callbackCounts[taskName] = 0;

            pollingService.CreateTask(
                taskName,
                TestAddresses,
                TimeSpan.FromMilliseconds(50),
                _ =>
                {
                    callbackCounts.AddOrUpdate(taskName, 1, (_, count) => count + 1);
                    Interlocked.Increment(ref metrics.CallbackInvocations);
                    return Task.CompletedTask;
                });

            Interlocked.Increment(ref metrics.TasksCreated);
            pollingService.StartTask(taskName);
        }

        await Task.Yield();
    }

    /// <summary>
    /// Ожидает получения хотя бы одного callback от каждой задачи.
    /// </summary>
    private async Task WaitForCallbacksAsync(
        ConcurrentDictionary<string, int> callbackCounts,
        CancellationToken ct)
    {
        var waitSw = Stopwatch.StartNew();
        const int maxWaitMs = 5000;

        while (waitSw.ElapsedMilliseconds < maxWaitMs)
        {
            ct.ThrowIfCancellationRequested();

            var tasksWithCallbacks = callbackCounts.Count(kv => kv.Value > 0);
            if (tasksWithCallbacks >= TaskCount)
            {
                break;
            }

            await Task.Delay(50, ct);
        }

        var finalCount = callbackCounts.Count(kv => kv.Value > 0);
        logger.LogInformation("Получены callbacks от {Count}/{Total} задач", finalCount, TaskCount);
    }

    #endregion

    #region Phase 6: Cleanup Verification

    /// <summary>
    /// Фаза 6: финальная проверка и очистка.
    /// </summary>
    private async Task RunPhase6CleanupVerificationAsync(StressTestMetrics metrics)
    {
        logger.LogInformation("═══ Фаза 6: Cleanup Verification ═══");

        await RemoveAllLoadTasksAsync();

        metrics.FinalTaskCount = pollingService.GetAllTasks().Count;
        logger.LogInformation("После очистки осталось {Count} задач", metrics.FinalTaskCount);
    }

    /// <summary>
    /// Удаляет все нагрузочные задачи.
    /// </summary>
    private async Task RemoveAllLoadTasksAsync()
    {
        for (var i = 0; i < TaskCount; i++)
        {
            await pollingService.RemoveTaskAsync($"load-task-{i}");
        }
    }

    #endregion

    #region Results Evaluation

    /// <summary>
    /// Оценивает результаты теста.
    /// </summary>
    private TestStepResult EvaluateResults(StressTestMetrics metrics)
    {
        var report = BuildMetricsReport(metrics);

        logger.LogInformation("═══ Результаты стресс-теста ═══\n{Report}", report);

        var failures = CollectFailures(metrics);

        if (failures.Count == 0)
        {
            return TestStepResult.Pass($"Все проверки пройдены. {report}");
        }

        var failureMessage = string.Join("; ", failures);
        return TestStepResult.Fail($"Обнаружены проблемы: {failureMessage}\n{report}");
    }

    /// <summary>
    /// Формирует отчёт метрик.
    /// </summary>
    private static string BuildMetricsReport(StressTestMetrics metrics)
    {
        return $"""
            TasksCreated: {metrics.TasksCreated}
            StartStopCycles: {metrics.StartStopCycles}
            CallbackInvocations: {metrics.CallbackInvocations}
            DeadlocksDetected: {metrics.DeadlocksDetected}
            MaxStopTimeMs: {metrics.MaxStopTimeMs}
            MemoryGrowthKB: {metrics.MemoryGrowthKb}
            DisconnectSimulations: {metrics.DisconnectSimulationCount}
            FinalTaskCount: {metrics.FinalTaskCount}
            UnhandledExceptions: {metrics.UnhandledExceptions}
            TotalDurationMs: {metrics.TotalDurationMs}
            """;
    }

    /// <summary>
    /// Собирает список нарушенных критериев.
    /// </summary>
    private static List<string> CollectFailures(StressTestMetrics metrics)
    {
        var failures = new List<string>();

        if (metrics.DeadlocksDetected > 0)
        {
            failures.Add($"deadlocksDetected={metrics.DeadlocksDetected}");
        }

        if (metrics.MaxStopTimeMs > DeadlockTimeoutMs)
        {
            failures.Add($"maxStopTimeMs={metrics.MaxStopTimeMs}>{DeadlockTimeoutMs}");
        }

        if (metrics.FinalTaskCount > 0)
        {
            failures.Add($"finalTaskCount={metrics.FinalTaskCount}");
        }

        if (metrics.MemoryGrowthKb > MaxMemoryGrowthKb)
        {
            failures.Add($"memoryGrowthKB={metrics.MemoryGrowthKb}>{MaxMemoryGrowthKb}");
        }

        if (metrics.UnhandledExceptions > 0)
        {
            failures.Add($"unhandledExceptions={metrics.UnhandledExceptions}");
        }

        return failures;
    }

    #endregion

    /// <summary>
    /// Метрики стресс-теста.
    /// </summary>
    private class StressTestMetrics
    {
        public int TasksCreated;
        public int StartStopCycles;
        public int CallbackInvocations;
        public int DeadlocksDetected;
        public long MaxStopTimeMs;
        public long MemoryGrowthKb;
        public int DisconnectSimulationCount;
        public int FinalTaskCount;
        public int UnhandledExceptions;
        public long TotalDurationMs;
    }
}

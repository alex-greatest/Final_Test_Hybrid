using System.Collections.Concurrent;
using AsyncAwaitBestPractices;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Protocol;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Diagnostic.Polling;

/// <summary>
/// Сервис управления задачами периодического опроса регистров ЭБУ котла.
/// </summary>
public class PollingService(
    RegisterReader reader,
    ILoggerFactory loggerFactory,
    ILogger<PollingService> logger,
    ITestStepLogger testStepLogger)
    : IAsyncDisposable
{
    private readonly DualLogger<PollingService> _logger = new(logger, testStepLogger);
    private readonly ConcurrentDictionary<string, PollingTask> _tasks = new();

    private bool _disposed;

    #region Task Creation

    /// <summary>
    /// Создаёт новую задачу опроса.
    /// </summary>
    /// <param name="name">Уникальное имя задачи.</param>
    /// <param name="addresses">Адреса регистров для опроса.</param>
    /// <param name="interval">Интервал опроса.</param>
    /// <param name="callback">Callback для обработки результатов.</param>
    /// <returns>Созданная задача опроса.</returns>
    public PollingTask CreateTask(
        string name,
        ushort[] addresses,
        TimeSpan interval,
        Func<Dictionary<ushort, object>, Task> callback)
    {
        var task = BuildPollingTask(name, addresses, interval, callback);

        RegisterTask(name, task);

        _logger.LogDebug("Создана задача опроса '{Name}' с интервалом {Interval}", name, interval);
        return task;
    }

    private PollingTask BuildPollingTask(
        string name,
        ushort[] addresses,
        TimeSpan interval,
        Func<Dictionary<ushort, object>, Task> callback)
    {
        return new PollingTask(
            name,
            addresses,
            interval,
            callback,
            reader,
            loggerFactory.CreateLogger<PollingTask>(),
            testStepLogger);
    }

    private void RegisterTask(string name, PollingTask task)
    {
        if (_tasks.TryAdd(name, task))
        {
            return;
        }

        DisposeTaskSafe(task);
        throw new InvalidOperationException($"Задача опроса с именем '{name}' уже существует");
    }

    private void DisposeTaskSafe(PollingTask task)
    {
        task.DisposeAsync().AsTask().SafeFireAndForget(ex =>
            _logger.LogWarning("Ошибка при dispose дублирующейся задачи опроса: {Error}", ex.Message));
    }

    #endregion

    #region Task Control

    /// <summary>
    /// Запускает задачу опроса по имени.
    /// </summary>
    public void StartTask(string name)
    {
        var task = FindTask(name);

        if (task != null)
        {
            task.Start();
        }
    }

    /// <summary>
    /// Останавливает задачу опроса по имени.
    /// </summary>
    public async Task StopTaskAsync(string name)
    {
        var task = FindTask(name);

        if (task != null)
        {
            await task.StopAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Останавливает все задачи опроса.
    /// </summary>
    public async Task StopAllTasksAsync()
    {
        _logger.LogDebug("Остановка всех задач опроса");

        var stopTasks = _tasks.Values.Select(task => task.StopAsync());
        await Task.WhenAll(stopTasks).ConfigureAwait(false);
    }

    private PollingTask? FindTask(string name)
    {
        if (_tasks.TryGetValue(name, out var task))
        {
            return task;
        }

        _logger.LogWarning("Задача опроса '{Name}' не найдена", name);
        return null;
    }

    #endregion

    #region Task Management

    /// <summary>
    /// Удаляет задачу опроса по имени.
    /// </summary>
    public async Task RemoveTaskAsync(string name)
    {
        if (!_tasks.TryRemove(name, out var task))
        {
            return;
        }

        await task.DisposeAsync().ConfigureAwait(false);
        _logger.LogDebug("Удалена задача опроса '{Name}'", name);
    }

    /// <summary>
    /// Возвращает список активных задач опроса.
    /// </summary>
    public IReadOnlyList<PollingTask> GetActiveTasks()
    {
        return _tasks.Values
            .Where(task => task.IsRunning)
            .ToList();
    }

    /// <summary>
    /// Возвращает все зарегистрированные задачи опроса.
    /// </summary>
    public IReadOnlyList<PollingTask> GetAllTasks()
    {
        return _tasks.Values.ToList();
    }

    #endregion

    #region Dispose

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        await StopAllTasksAsync().ConfigureAwait(false);
        await DisposeAllTasksAsync().ConfigureAwait(false);

        _tasks.Clear();
    }

    private async Task DisposeAllTasksAsync()
    {
        foreach (var task in _tasks.Values)
        {
            await task.DisposeAsync().ConfigureAwait(false);
        }
    }

    #endregion
}
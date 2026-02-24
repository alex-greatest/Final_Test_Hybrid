namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Timing;

public interface ITimerService
{
    event Action? OnChanged;
    void Start(string key);
    /// <summary>
    /// Останавливает таймер и переводит его в замороженное состояние до <see cref="Clear"/>.
    /// </summary>
    /// <param name="key">Ключ таймера.</param>
    /// <returns>
    /// Значение таймера:
    /// для running-таймера фиксирует текущий elapsed и сохраняет его в frozen-хранилище;
    /// для frozen-таймера возвращает ранее сохранённое значение без удаления;
    /// если таймер не найден — <see langword="null"/>.
    /// </returns>
    TimeSpan? Stop(string key);
    TimeSpan? GetElapsed(string key);
    bool IsRunning(string key);
    /// <summary>
    /// Возвращает все таймеры, активные для UI: и running, и frozen (до вызова <see cref="Clear"/>).
    /// </summary>
    IReadOnlyDictionary<string, TimeSpan> GetAllActive();
    void StopAll();
    void Clear();
}

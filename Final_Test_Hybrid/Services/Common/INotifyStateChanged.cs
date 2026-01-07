namespace Final_Test_Hybrid.Services.Common;

/// <summary>
/// Унифицированный интерфейс для компонентов, которые уведомляют об изменении состояния.
/// Позволяет использовать StateChangeAggregator для подписки на несколько источников.
/// </summary>
public interface INotifyStateChanged
{
    /// <summary>
    /// Событие, вызываемое при изменении состояния.
    /// </summary>
    event Action? OnStateChanged;
}

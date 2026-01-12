namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;

/// <summary>
/// Базовый интерфейс для компонентов, требующих валидацию PLC тегов.
/// </summary>
public interface IRequiresPlcTags
{
    /// <summary>
    /// Список тегов, которые должны существовать в PLC.
    /// </summary>
    IReadOnlyList<string> RequiredPlcTags { get; }
}

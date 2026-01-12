namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;

/// <summary>
/// Базовый интерфейс для компонентов с PLC блоком.
/// Используется для установки Selected при ошибках.
/// </summary>
public interface IHasPlcBlockPath
{
    /// <summary>
    /// Путь к блоку в PLC (например, "DB_VI.Block_Boiler_Adapter").
    /// </summary>
    string PlcBlockPath { get; }
}

namespace Final_Test_Hybrid.Components.Schemes;

/// <summary>
/// Информация о клапане на схеме.
/// </summary>
public class ValveInfo
{
    /// <summary>
    /// Идентификатор клапана (например, "SF0.1").
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Координата X центра клапана.
    /// </summary>
    public double X { get; init; }

    /// <summary>
    /// Координата Y центра клапана.
    /// </summary>
    public double Y { get; init; }

    /// <summary>
    /// Ширина кликабельной области.
    /// </summary>
    public double Width { get; init; } = 100;

    /// <summary>
    /// Высота кликабельной области.
    /// </summary>
    public double Height { get; init; } = 60;

    /// <summary>
    /// Тип клапана.
    /// </summary>
    public ValveType Type { get; init; }

    /// <summary>
    /// Текущее состояние клапана.
    /// </summary>
    public bool IsOpen { get; set; }

    /// <summary>
    /// Выбран ли клапан.
    /// </summary>
    public bool IsSelected { get; set; }

    /// <summary>
    /// Есть ли ошибка на клапане.
    /// </summary>
    public bool HasError { get; set; }
}

/// <summary>
/// Тип клапана.
/// </summary>
public enum ValveType
{
    /// <summary>
    /// Соленоидный клапан (SF).
    /// </summary>
    Solenoid,

    /// <summary>
    /// Регулирующий клапан (VNR).
    /// </summary>
    Regulating,

    /// <summary>
    /// Пропорциональный клапан (VP).
    /// </summary>
    Proportional,

    /// <summary>
    /// Регулятор давления (RG).
    /// </summary>
    PressureRegulator,

    /// <summary>
    /// Электроклапан (EV).
    /// </summary>
    Electrovalve,

    /// <summary>
    /// Моторизованный клапан (VM).
    /// </summary>
    Motorized,

    /// <summary>
    /// Клапан давления (VPP).
    /// </summary>
    PressureValve
}

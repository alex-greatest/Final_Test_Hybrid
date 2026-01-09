namespace Final_Test_Hybrid.Services.Diagnostic.Models;

/// <summary>
/// Информация об ошибке котла.
/// </summary>
/// <param name="Id">ID ошибки (1-25).</param>
/// <param name="DisplayCode">Код на дисплее котла (E1, E7, A4, CE и т.д.).</param>
/// <param name="Description">Описание ошибки на русском языке.</param>
public record BoilerErrorInfo(ushort Id, string DisplayCode, string Description);

/// <summary>
/// Справочник ошибок котла (Таблица 2 - ID ошибок из протокола v1.8.10).
/// </summary>
public static class BoilerErrors
{
    private static readonly Dictionary<ushort, BoilerErrorInfo> Errors = new()
    {
        [0] = new BoilerErrorInfo(0, "", "Нет ошибки"),
        [1] = new BoilerErrorInfo(1, "E1", "Блокировка при перегреве"),
        [2] = new BoilerErrorInfo(2, "E2", "Блокировка зажигания"),
        [3] = new BoilerErrorInfo(3, "E3", "Неисправность датчика температуры подающей линии"),
        [4] = new BoilerErrorInfo(4, "A4", "Неисправность датчика температуры ГВС"),
        [5] = new BoilerErrorInfo(5, "Ac", "Неисправность датчика температуры бойлера косвенного нагрева"),
        [6] = new BoilerErrorInfo(6, "A8", "Неисправен датчик наружной температуры"),
        [7] = new BoilerErrorInfo(7, "E7", "Не обнаружен тахосигнал с вентилятора"),
        [8] = new BoilerErrorInfo(8, "CE", "Пневматический выключатель не закрывается"),
        [9] = new BoilerErrorInfo(9, "CP", "Пневматический выключатель закрыт до начала нагрева жидкого теплоносителя"),
        [10] = new BoilerErrorInfo(10, "CF", "Вентилятор не может достичь заданных оборотов"),
        [11] = new BoilerErrorInfo(11, "FA", "Неисправность входного и (или) выходного клапанов регулятора давления газа"),
        [12] = new BoilerErrorInfo(12, "d1", "Неисправность модулирующей катушки регулятора давления газа"),
        [13] = new BoilerErrorInfo(13, "FL", "Неисправность датчика контроля пламени"),
        [14] = new BoilerErrorInfo(14, "CE", "Низкое давление воды в системе"),
        [15] = new BoilerErrorInfo(15, "CA", "Высокое давление воды в системе"),
        [16] = new BoilerErrorInfo(16, "P", "Не задан тип котла"),
        [17] = new BoilerErrorInfo(17, "H", "Не задана ступень вентилятора"),
        [18] = new BoilerErrorInfo(18, "F0", "Залипание кнопок"),
        [19] = new BoilerErrorInfo(19, "LA", "Не достигается температура для проведения термической дезинфекции"),
        [20] = new BoilerErrorInfo(20, "PE", "Ошибка работы насоса, связанная с электропитанием"),
        [21] = new BoilerErrorInfo(21, "Pr", "Ошибка работы насоса, связанная с возможным отсутствием поступления жидкости в ротор"),
        [22] = new BoilerErrorInfo(22, "P8", "Ошибка работы насоса, связанная с блокировкой ротора"),
        [23] = new BoilerErrorInfo(23, "rE", "Неисправность катушек клапанов регулятора давления газа"),
        [24] = new BoilerErrorInfo(24, "RA", "Невозможность нагрева бойлера косвенного нагрева из режима защиты от замерзания"),
        [25] = new BoilerErrorInfo(25, "IE", "Внутренняя ошибка ЭБУ")
    };

    /// <summary>
    /// Получает информацию об ошибке по её ID.
    /// </summary>
    /// <param name="id">ID ошибки (0-25).</param>
    /// <returns>Информация об ошибке. Для неизвестных ID возвращает запись с описанием "Неизвестная ошибка".</returns>
    public static BoilerErrorInfo Get(ushort id) =>
        Errors.TryGetValue(id, out var info)
            ? info
            : new BoilerErrorInfo(id, $"?{id}", $"Неизвестная ошибка ({id})");

    /// <summary>
    /// Информация об отсутствии ошибки (ID = 0).
    /// </summary>
    public static BoilerErrorInfo None => Errors[0];
}

namespace Final_Test_Hybrid.Models.Errors;

public static partial class ErrorDefinitions
{
    // Ошибки ЭБУ котла (ID 1-25)
    public static readonly ErrorDefinition EcuE9 = new("ЭБУ-E9", "Блокировка при перегреве", Severity: ErrorSeverity.Critical);
    public static readonly ErrorDefinition EcuEA = new("ЭБУ-EA", "Блокировка зажигания", Severity: ErrorSeverity.Critical);
    public static readonly ErrorDefinition EcuE2 = new("ЭБУ-E2", "Неисправность датчика температуры подающей линии", Severity: ErrorSeverity.Critical);
    public static readonly ErrorDefinition EcuA7 = new("ЭБУ-A7", "Неисправность датчика температуры ГВС", Severity: ErrorSeverity.Critical);
    public static readonly ErrorDefinition EcuAd = new("ЭБУ-Ad", "Неисправность датчика температуры бойлера косвенного нагрева", Severity: ErrorSeverity.Critical);
    public static readonly ErrorDefinition EcuA8 = new("ЭБУ-A8", "Неисправен датчик наружной температуры", Severity: ErrorSeverity.Critical);
    public static readonly ErrorDefinition EcuC7 = new("ЭБУ-C7", "Не обнаружен тахосигнал с вентилятора", Severity: ErrorSeverity.Critical);
    public static readonly ErrorDefinition EcuC6 = new("ЭБУ-C6", "Пневматический выключатель не закрывается", Severity: ErrorSeverity.Critical);
    public static readonly ErrorDefinition EcuC4 = new("ЭБУ-C4", "Пневматический выключатель закрыт до начала нагрева", Severity: ErrorSeverity.Critical);
    public static readonly ErrorDefinition EcuC1 = new("ЭБУ-C1", "Вентилятор не может достичь заданных оборотов", Severity: ErrorSeverity.Critical);
    public static readonly ErrorDefinition EcuFA = new("ЭБУ-FA", "Неисправность клапанов регулятора давления газа", Severity: ErrorSeverity.Critical);
    public static readonly ErrorDefinition EcuD7 = new("ЭБУ-D7", "Неисправность модулирующей катушки регулятора давления газа", Severity: ErrorSeverity.Critical);
    public static readonly ErrorDefinition EcuFL = new("ЭБУ-FL", "Неисправность датчика контроля пламени", Severity: ErrorSeverity.Critical);
    public static readonly ErrorDefinition EcuCE = new("ЭБУ-CE", "Низкое давление воды в системе", Severity: ErrorSeverity.Critical);
    public static readonly ErrorDefinition EcuCA = new("ЭБУ-CA", "Высокое давление воды в системе", Severity: ErrorSeverity.Critical);
    public static readonly ErrorDefinition EcuP = new("ЭБУ-P", "Не задан тип котла", Severity: ErrorSeverity.Critical);
    public static readonly ErrorDefinition Ecu11 = new("ЭБУ-11", "Не задана ступень вентилятора", Severity: ErrorSeverity.Critical);
    public static readonly ErrorDefinition EcuFD = new("ЭБУ-FD", "Залипание кнопок", Severity: ErrorSeverity.Critical);
    public static readonly ErrorDefinition EcuLA = new("ЭБУ-LA", "Не достигается температура для термической дезинфекции", Severity: ErrorSeverity.Critical);
    public static readonly ErrorDefinition EcuPE = new("ЭБУ-PE", "Ошибка работы насоса (электропитание)", Severity: ErrorSeverity.Critical);
    public static readonly ErrorDefinition EcuPd = new("ЭБУ-Pd", "Ошибка работы насоса (отсутствие жидкости)", Severity: ErrorSeverity.Critical);
    public static readonly ErrorDefinition EcuPA = new("ЭБУ-PA", "Ошибка работы насоса (блокировка ротора)", Severity: ErrorSeverity.Critical);
    public static readonly ErrorDefinition EcuF7 = new("ЭБУ-F7", "Неисправность катушек клапанов регулятора давления газа", Severity: ErrorSeverity.Critical);
    public static readonly ErrorDefinition EcuA9 = new("ЭБУ-A9", "Невозможность нагрева бойлера из режима защиты от замерзания", Severity: ErrorSeverity.Critical);
    public static readonly ErrorDefinition EcuIE = new("ЭБУ-IE", "Внутренняя ошибка ЭБУ", Severity: ErrorSeverity.Critical);

    internal static IEnumerable<ErrorDefinition> DiagnosticEcuErrors =>
    [
        EcuE9, EcuEA, EcuE2, EcuA7, EcuAd, EcuA8, EcuC7, EcuC6, EcuC4, EcuC1,
        EcuFA, EcuD7, EcuFL, EcuCE, EcuCA, EcuP, Ecu11, EcuFD, EcuLA, EcuPE,
        EcuPd, EcuPA, EcuF7, EcuA9, EcuIE
    ];

    /// <summary>
    /// Получить ErrorDefinition по ID ошибки ЭБУ (1-25).
    /// </summary>
    /// <param name="errorId">ID ошибки (1-25).</param>
    /// <returns>ErrorDefinition или null для неизвестных ID.</returns>
    public static ErrorDefinition? GetEcuErrorById(ushort errorId) => errorId switch
    {
        1 => EcuE9, 2 => EcuEA, 3 => EcuE2, 4 => EcuA7, 5 => EcuAd,
        6 => EcuA8, 7 => EcuC7, 8 => EcuC6, 9 => EcuC4, 10 => EcuC1,
        11 => EcuFA, 12 => EcuD7, 13 => EcuFL, 14 => EcuCE, 15 => EcuCA,
        16 => EcuP, 17 => Ecu11, 18 => EcuFD, 19 => EcuLA, 20 => EcuPE,
        21 => EcuPd, 22 => EcuPA, 23 => EcuF7, 24 => EcuA9, 25 => EcuIE,
        _ => null
    };
}

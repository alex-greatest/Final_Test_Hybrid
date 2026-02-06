using Final_Test_Hybrid.Models.Errors;

namespace Final_Test_Hybrid.Services.Steps.Steps.Coms;

public partial class ReadSoftCodePlugStep
{
    private static VerifyUInt16Action BuildCurrentOffsetAction()
    {
        return CreateVerifyUInt16Action(
            stepNo: 13,
            title: "Проверка сдвига тока",
            register: RegisterCurrentOffset,
            expectedRecipeKey: CurrentOffsetRecipe,
            minRecipeKey: CurrentOffsetMinRecipe,
            maxRecipeKey: CurrentOffsetMaxRecipe,
            resultName: CurrentOffsetResultName,
            unit: "мА",
            mismatchError: ErrorDefinitions.EcuCurrentOffsetMismatch,
            readLogMessage: $"Чтение сдвига тока на модуляционной катушке из регистра {RegisterCurrentOffset}",
            readErrorPrefix: $"Ошибка при чтении сдвига тока на модуляционной катушке из регистра {RegisterCurrentOffset}. ",
            statusLogTemplate: "Сдвиг тока: {0} мА, ожидалось: {1} мА, статус: {2}",
            mismatchTemplate: "Сдвиг тока на модуляционной катушке в ЭБУ ({0} мА) не совпадает с ожидаемым ({1} мА)");
    }

    private static VerifyFloatAction BuildFlowCoefficientAction()
    {
        return CreateVerifyFloatAction(
            stepNo: 14,
            title: "Проверка коэффициента k расхода воды",
            startRegister: RegisterFlowCoefficientHi,
            registerCount: 2,
            expectedRecipeKey: FlowCoefficientRecipe,
            minRecipeKey: FlowCoefficientMinRecipe,
            maxRecipeKey: FlowCoefficientMaxRecipe,
            resultName: FlowCoefficientResultName,
            unit: "",
            resultFormat: "F3",
            mismatchError: ErrorDefinitions.EcuFlowCoefficientMismatch,
            readLogMessage: $"Чтение коэффициента k расхода воды из регистров {RegisterFlowCoefficientHi}-{RegisterFlowCoefficientHi + 1}",
            readErrorPrefix: $"Ошибка при чтении коэффициента k расхода воды из регистров {RegisterFlowCoefficientHi}-{RegisterFlowCoefficientHi + 1}. ",
            statusLogTemplate: "Коэффициент k: {0:F3}, ожидалось: {1:F3}, статус: {2}",
            mismatchTemplate: "Коэффициент k расхода воды в ЭБУ ({0:F3}) не совпадает с ожидаемым ({1:F3})",
            shouldRun: IsDualCircuit,
            skipLogMessage: "Пропуск чтения коэффициента k — одноконтурный котёл");
    }

    private static VerifyUInt16Action BuildMaxPumpAutoPowerAction()
    {
        return CreateVerifyUInt16Action(
            stepNo: 15,
            title: "Проверка макс. мощности насоса в авто режиме",
            register: RegisterPumpPowerMax,
            expectedRecipeKey: MaxPumpAutoPowerRecipe,
            minRecipeKey: MaxPumpAutoPowerMinRecipe,
            maxRecipeKey: MaxPumpAutoPowerMaxRecipe,
            resultName: MaxPumpAutoPowerResultName,
            unit: "Вт",
            mismatchError: ErrorDefinitions.EcuMaxPumpAutoPowerMismatch,
            readLogMessage: $"Чтение макс. мощности насоса в авто режиме из регистра {RegisterPumpPowerMax}",
            readErrorPrefix: $"Ошибка при чтении макс. мощности насоса в авто режиме из регистра {RegisterPumpPowerMax}. ",
            statusLogTemplate: "Макс. мощность насоса в авто режиме: {0} Вт, ожидалось: {1} Вт, статус: {2}",
            mismatchTemplate: "Макс. мощность насоса в авто режиме в ЭБУ ({0} Вт) не совпадает с ожидаемым ({1} Вт)");
    }

    private static VerifyUInt16Action BuildMinPumpAutoPowerAction()
    {
        return CreateVerifyUInt16Action(
            stepNo: 16,
            title: "Проверка мин. мощности насоса в авто режиме",
            register: RegisterPumpPowerMin,
            expectedRecipeKey: MinPumpAutoPowerRecipe,
            minRecipeKey: MinPumpAutoPowerMinRecipe,
            maxRecipeKey: MinPumpAutoPowerMaxRecipe,
            resultName: MinPumpAutoPowerResultName,
            unit: "Вт",
            mismatchError: ErrorDefinitions.EcuMinPumpAutoPowerMismatch,
            readLogMessage: $"Чтение мин. мощности насоса в авто режиме из регистра {RegisterPumpPowerMin}",
            readErrorPrefix: $"Ошибка при чтении мин. мощности насоса в авто режиме из регистра {RegisterPumpPowerMin}. ",
            statusLogTemplate: "Мин. мощность насоса в авто режиме: {0} Вт, ожидалось: {1} Вт, статус: {2}",
            mismatchTemplate: "Мин. мощность насоса в авто режиме в ЭБУ ({0} Вт) не совпадает с ожидаемым ({1} Вт)");
    }

    private static VerifyUInt16Action BuildComfortHysteresisAction()
    {
        return CreateVerifyUInt16Action(
            stepNo: 17,
            title: "Проверка гистерезиса ГВС",
            register: RegisterComfortHysteresis,
            expectedRecipeKey: ComfortHysteresisRecipe,
            minRecipeKey: ComfortHysteresisMinRecipe,
            maxRecipeKey: ComfortHysteresisMaxRecipe,
            resultName: ComfortHysteresisResultName,
            unit: "°С",
            mismatchError: ErrorDefinitions.EcuComfortHysteresisMismatch,
            readLogMessage: $"Чтение гистерезиса ГВС в режиме комфорт из регистра {RegisterComfortHysteresis}",
            readErrorPrefix: $"Ошибка при чтении гистерезиса ГВС в режиме комфорт из регистра {RegisterComfortHysteresis}. ",
            statusLogTemplate: "Гистерезис ГВС: {0} °С, ожидалось: {1} °С, статус: {2}",
            mismatchTemplate: "Гистерезис ГВС в режиме комфорт в ЭБУ ({0} °С) не совпадает с ожидаемым ({1} °С)",
            shouldRun: IsDualCircuit,
            skipLogMessage: "Пропуск чтения гистерезиса ГВС — одноконтурный котёл");
    }

    private static VerifyUInt16Action BuildMaxFlowTemperatureAction()
    {
        return CreateVerifyUInt16Action(
            stepNo: 18,
            title: "Проверка макс. температуры подающей линии",
            register: RegisterMaxFlowTemperature,
            expectedRecipeKey: MaxFlowTemperatureRecipe,
            minRecipeKey: MaxFlowTemperatureMinRecipe,
            maxRecipeKey: MaxFlowTemperatureMaxRecipe,
            resultName: MaxFlowTemperatureResultName,
            unit: "°С",
            mismatchError: ErrorDefinitions.EcuMaxFlowTemperatureMismatch,
            readLogMessage: $"Чтение макс. температуры подающей линии из регистра {RegisterMaxFlowTemperature}",
            readErrorPrefix: $"Ошибка при чтении макс. температуры подающей линии из регистра {RegisterMaxFlowTemperature}. ",
            statusLogTemplate: "Макс. температура подающей линии: {0} °С, ожидалось: {1} °С, статус: {2}",
            mismatchTemplate: "Макс. температура подающей линии в ЭБУ ({0} °С) не совпадает с ожидаемым ({1} °С)");
    }

    private static ReadOnlyStringAction BuildItelmaArticleAction()
    {
        return CreateReadOnlyStringAction(
            stepNo: 19,
            title: "Чтение артикула ИТЭЛМА",
            startRegister: RegisterItelmaArticle,
            registerCount: 7,
            maxLength: NomenclatureMaxLength,
            resultName: ItelmaArticleResultName,
            readLogMessage: $"Чтение артикула ИТЭЛМА из регистров {RegisterItelmaArticle}-{RegisterItelmaArticle + 6}",
            readErrorPrefix: $"Ошибка при чтении артикула ИТЭЛМА из регистров {RegisterItelmaArticle}-{RegisterItelmaArticle + 6}. ",
            valueLogTemplate: "Артикул ИТЭЛМА: {0}");
    }

    private static ReadOnlyStringAction BuildProductionDateAction()
    {
        return CreateReadOnlyStringAction(
            stepNo: 20,
            title: "Чтение даты производства",
            startRegister: RegisterProductionDate,
            registerCount: 4,
            maxLength: ProductionDateMaxLength,
            resultName: ProductionDateResultName,
            readLogMessage: $"Чтение даты производства из регистров {RegisterProductionDate}-{RegisterProductionDate + 3}",
            readErrorPrefix: $"Ошибка при чтении даты производства из регистров {RegisterProductionDate}-{RegisterProductionDate + 3}. ",
            valueLogTemplate: "Дата производства: {0}");
    }

    private static ReadOnlyUInt32Action BuildSupplierCodeAction()
    {
        return CreateReadOnlyUInt32Action(
            stepNo: 21,
            title: "Чтение кода поставщика",
            startRegister: RegisterSupplierCodeHi,
            registerCount: 2,
            resultName: SupplierCodeResultName,
            readLogMessage: $"Чтение кода поставщика из регистров {RegisterSupplierCodeHi}-{RegisterSupplierCodeHi + 1}",
            readErrorPrefix: $"Ошибка при чтении кода поставщика из регистров {RegisterSupplierCodeHi}-{RegisterSupplierCodeHi + 1}. ",
            valueLogTemplate: "Код поставщика: {0}");
    }

    private static ReadOnlyUInt32Action BuildCounterNumberAction()
    {
        return CreateReadOnlyUInt32Action(
            stepNo: 22,
            title: "Чтение порядкового номера",
            startRegister: RegisterCounterNumberHi,
            registerCount: 2,
            resultName: CounterNumberResultName,
            readLogMessage: $"Чтение порядкового номера из регистров {RegisterCounterNumberHi}-{RegisterCounterNumberHi + 1}",
            readErrorPrefix: $"Ошибка при чтении порядкового номера из регистров {RegisterCounterNumberHi}-{RegisterCounterNumberHi + 1}. ",
            valueLogTemplate: "Порядковый номер: {0}");
    }

    private static ThermostatJumperCheckAction BuildThermostatJumperAction()
    {
        return new ThermostatJumperCheckAction(
            StepNo: 23,
            Title: "Проверка перемычки термостата",
            Register: RegisterThermostatJumper,
            ReadLogMessage: $"Проверка перемычки термостата из регистра {RegisterThermostatJumper}",
            ReadErrorPrefix: $"Ошибка при чтении перемычки термостата из регистра {RegisterThermostatJumper}. ",
            StatusLogTemplate: "Перемычка термостата: {0} ({1})",
            MissingMessage: "Не установлена перемычка термостата (значение: 0)",
            MissingError: ErrorDefinitions.ThermostatJumperMissing);
    }
}

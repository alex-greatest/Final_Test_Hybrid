using Final_Test_Hybrid.Models.Errors;

namespace Final_Test_Hybrid.Services.Steps.Steps.Coms;

public partial class ReadSoftCodePlugStep
{
    private static VerifyConnectionType1054Action BuildConnectionTypeAction()
    {
        return new VerifyConnectionType1054Action(
            StepNo: 1,
            Title: "Проверка типа подключения к котлу (1054)",
            Register: RegisterConnectionType,
            ExpectedRecipeKey: NumberOfContoursRecipe,
            ReadLogMessage: $"Чтение типа подключения к котлу из регистра {RegisterConnectionType}",
            ReadErrorPrefix: $"Ошибка при чтении типа подключения к котлу из регистра {RegisterConnectionType}. ",
            MismatchMessage: "1. Отсканированный код на котле не соответствует котлу;\n2. На котёл установлен не правильный жгут;\n3. Жгут повреждён.",
            MismatchError: ErrorDefinitions.EcuConnectionTypeMismatch);
    }

    private static VerifyStringAction BuildArticleAction()
    {
        return CreateVerifyStringAction(
            stepNo: 2,
            title: "Проверка артикула",
            startRegister: RegisterNomenclatureNumber,
            registerCount: 7,
            maxLength: NomenclatureMaxLength,
            usesBoilerArticle: true,
            expectedRecipeKey: null,
            resultName: ArticleResultName,
            mismatchError: ErrorDefinitions.EcuArticleMismatch,
            readLogMessage: $"Чтение артикула из регистров {RegisterNomenclatureNumber}-{RegisterNomenclatureNumber + 6}",
            readErrorPrefix: $"Ошибка при чтении артикула из регистров {RegisterNomenclatureNumber}-{RegisterNomenclatureNumber + 6}. ",
            statusLogTemplate: "Артикул: {0}, ожидался: {1}, статус: {2}",
            mismatchTemplate: "Артикул в ЭБУ ({0}) не совпадает с ожидаемым ({1})");
    }

    private static VerifyUInt16Action BuildBoilerTypeAction()
    {
        return CreateVerifyUInt16Action(
            stepNo: 3,
            title: "Проверка типа котла",
            register: RegisterBoilerPowerType,
            expectedRecipeKey: BoilerTypeRecipe,
            minRecipeKey: BoilerTypeMinRecipe,
            maxRecipeKey: BoilerTypeMaxRecipe,
            resultName: BoilerTypeResultName,
            unit: "",
            mismatchError: ErrorDefinitions.EcuBoilerTypeMismatch,
            readLogMessage: $"Чтение типа котла из регистра {RegisterBoilerPowerType}",
            readErrorPrefix: $"Ошибка при чтении типа котла из регистра {RegisterBoilerPowerType}. ",
            statusLogTemplate: "Тип котла: {0}, ожидался: {1}, статус: {2}",
            mismatchTemplate: "Тип котла в ЭБУ ({0}) не совпадает с ожидаемым ({1})");
    }

    private static VerifyUInt16Action BuildPumpTypeAction()
    {
        return CreateVerifyUInt16Action(
            stepNo: 4,
            title: "Проверка типа насоса",
            register: RegisterPumpType,
            expectedRecipeKey: PumpTypeRecipe,
            minRecipeKey: PumpTypeMinRecipe,
            maxRecipeKey: PumpTypeMaxRecipe,
            resultName: PumpTypeResultName,
            unit: "",
            mismatchError: ErrorDefinitions.EcuPumpTypeMismatch,
            readLogMessage: $"Чтение типа насоса из регистра {RegisterPumpType}",
            readErrorPrefix: $"Ошибка при чтении типа насоса из регистра {RegisterPumpType}. ",
            statusLogTemplate: "Тип насоса: {0}, ожидался: {1}, статус: {2}",
            mismatchTemplate: "Тип насоса в ЭБУ ({0}) не совпадает с ожидаемым ({1})");
    }

    private static VerifyUInt16Action BuildPressureSensorTypeAction()
    {
        return CreateVerifyUInt16Action(
            stepNo: 5,
            title: "Проверка типа датчика давления",
            register: RegisterPressureDeviceType,
            expectedRecipeKey: PresSenTypeRecipe,
            minRecipeKey: PresSensorMinRecipe,
            maxRecipeKey: PresSensorMaxRecipe,
            resultName: PresSensorResultName,
            unit: "",
            mismatchError: ErrorDefinitions.EcuPressureDeviceTypeMismatch,
            readLogMessage: $"Чтение типа датчика давления из регистра {RegisterPressureDeviceType}",
            readErrorPrefix: $"Ошибка при чтении типа датчика давления из регистра {RegisterPressureDeviceType}. ",
            statusLogTemplate: "Тип датчика давления: {0}, ожидался: {1}, статус: {2}",
            mismatchTemplate: "Тип датчика давления в ЭБУ ({0}) не совпадает с ожидаемым ({1})");
    }

    private static VerifyUInt16Action BuildGasRegulatorTypeAction()
    {
        return CreateVerifyUInt16Action(
            stepNo: 6,
            title: "Проверка типа регулятора газа",
            register: RegisterGasRegulatorType,
            expectedRecipeKey: GasValveTypeRecipe,
            minRecipeKey: GasValveTypeMinRecipe,
            maxRecipeKey: GasValveTypeMaxRecipe,
            resultName: GasValveTypeResultName,
            unit: "",
            mismatchError: ErrorDefinitions.EcuGasRegulatorTypeMismatch,
            readLogMessage: $"Чтение типа регулятора газа из регистра {RegisterGasRegulatorType}",
            readErrorPrefix: $"Ошибка при чтении типа регулятора газа из регистра {RegisterGasRegulatorType}. ",
            statusLogTemplate: "Тип регулятора газа: {0}, ожидался: {1}, статус: {2}",
            mismatchTemplate: "Тип регулятора газа в ЭБУ ({0}) не совпадает с ожидаемым ({1})");
    }

    private static VerifyUInt16Action BuildMaxChHeatOutputAction()
    {
        return CreateVerifyUInt16Action(
            stepNo: 7,
            title: "Проверка макс. теплопроизводительности отопления",
            register: RegisterMaxHeatOutputCh,
            expectedRecipeKey: MaxChHeatOutRecipe,
            minRecipeKey: MaxChHeatOutMinRecipe,
            maxRecipeKey: MaxChHeatOutMaxRecipe,
            resultName: MaxChHeatOutResultName,
            unit: "%",
            mismatchError: ErrorDefinitions.EcuMaxChHeatOutputMismatch,
            readLogMessage: $"Чтение макс. теплопроизводительности отопления из регистра {RegisterMaxHeatOutputCh}",
            readErrorPrefix: $"Ошибка при чтении макс. теплопроизводительности отопления из регистра {RegisterMaxHeatOutputCh}. ",
            statusLogTemplate: "Макс. теплопроизводительность отопления: {0}%, ожидалось: {1}%, статус: {2}",
            mismatchTemplate: "Макс. теплопроизводительность отопления в ЭБУ ({0}%) не совпадает с ожидаемым ({1}%)");
    }

    private static VerifyUInt16Action BuildMaxDhwHeatOutputAction()
    {
        return CreateVerifyUInt16Action(
            stepNo: 8,
            title: "Проверка макс. теплопроизводительности ГВС",
            register: RegisterMaxHeatOutputDhw,
            expectedRecipeKey: MaxDhwHeatOutRecipe,
            minRecipeKey: MaxDhwHeatOutMinRecipe,
            maxRecipeKey: MaxDhwHeatOutMaxRecipe,
            resultName: MaxDhwHeatOutResultName,
            unit: "%",
            mismatchError: ErrorDefinitions.EcuMaxDhwHeatOutputMismatch,
            readLogMessage: $"Чтение макс. теплопроизводительности ГВС из регистра {RegisterMaxHeatOutputDhw}",
            readErrorPrefix: $"Ошибка при чтении макс. теплопроизводительности ГВС из регистра {RegisterMaxHeatOutputDhw}. ",
            statusLogTemplate: "Макс. теплопроизводительность ГВС: {0}%, ожидалось: {1}%, статус: {2}",
            mismatchTemplate: "Макс. теплопроизводительность ГВС в ЭБУ ({0}%) не совпадает с ожидаемым ({1}%)");
    }

    private static VerifyUInt16Action BuildMinChHeatOutputAction()
    {
        return CreateVerifyUInt16Action(
            stepNo: 9,
            title: "Проверка мин. теплопроизводительности отопления",
            register: RegisterMinHeatOutputCh,
            expectedRecipeKey: MinChHeatOutRecipe,
            minRecipeKey: MinChHeatOutMinRecipe,
            maxRecipeKey: MinChHeatOutMaxRecipe,
            resultName: MinChHeatOutResultName,
            unit: "%",
            mismatchError: ErrorDefinitions.EcuMinChHeatOutputMismatch,
            readLogMessage: $"Чтение мин. теплопроизводительности отопления из регистра {RegisterMinHeatOutputCh}",
            readErrorPrefix: $"Ошибка при чтении мин. теплопроизводительности отопления из регистра {RegisterMinHeatOutputCh}. ",
            statusLogTemplate: "Мин. теплопроизводительность отопления: {0}%, ожидалось: {1}%, статус: {2}",
            mismatchTemplate: "Мин. теплопроизводительность отопления в ЭБУ ({0}%) не совпадает с ожидаемым ({1}%)");
    }

    private static VerifyUInt16Action BuildPumpModeAction()
    {
        return CreateVerifyUInt16Action(
            stepNo: 10,
            title: "Проверка режима работы насоса",
            register: RegisterPumpMode,
            expectedRecipeKey: PumpModeRecipe,
            minRecipeKey: PumpModeMinRecipe,
            maxRecipeKey: PumpModeMaxRecipe,
            resultName: PumpModeResultName,
            unit: "",
            mismatchError: ErrorDefinitions.EcuPumpModeMismatch,
            readLogMessage: $"Чтение режима работы насоса из регистра {RegisterPumpMode}",
            readErrorPrefix: $"Ошибка при чтении режима работы насоса из регистра {RegisterPumpMode}. ",
            statusLogTemplate: "Режим работы насоса: {0}, ожидался: {1}, статус: {2}",
            mismatchTemplate: "Режим работы насоса в ЭБУ ({0}) не совпадает с ожидаемым ({1})");
    }

    private static VerifyUInt16Action BuildPumpPowerAction()
    {
        return CreateVerifyUInt16Action(
            stepNo: 11,
            title: "Проверка установленной мощности насоса",
            register: RegisterPumpPower,
            expectedRecipeKey: PumpPowerRecipe,
            minRecipeKey: PumpPowerMinRecipe,
            maxRecipeKey: PumpPowerMaxRecipe,
            resultName: PumpPowerResultName,
            unit: "",
            mismatchError: ErrorDefinitions.EcuPumpPowerMismatch,
            readLogMessage: $"Чтение установленной мощности насоса из регистра {RegisterPumpPower}",
            readErrorPrefix: $"Ошибка при чтении установленной мощности насоса из регистра {RegisterPumpPower}. ",
            statusLogTemplate: "Установленная мощность насоса: {0}, ожидалось: {1}, статус: {2}",
            mismatchTemplate: "Установленная мощность насоса в ЭБУ ({0}) не совпадает с ожидаемым ({1})");
    }

    private static VerifyUInt16Action BuildGasTypeAction()
    {
        return CreateVerifyUInt16Action(
            stepNo: 12,
            title: "Проверка вида газа",
            register: RegisterGasType,
            expectedRecipeKey: GasTypeRecipe,
            minRecipeKey: GasTypeMinRecipe,
            maxRecipeKey: GasTypeMaxRecipe,
            resultName: GasTypeResultName,
            unit: "",
            mismatchError: ErrorDefinitions.EcuGasTypeMismatch,
            readLogMessage: $"Чтение вида подаваемого газа из регистра {RegisterGasType}",
            readErrorPrefix: $"Ошибка при чтении вида подаваемого газа из регистра {RegisterGasType}. ",
            statusLogTemplate: "Вид подаваемого газа: {0}, ожидался: {1}, статус: {2}",
            mismatchTemplate: "Вид подаваемого газа в ЭБУ ({0}) не совпадает с ожидаемым ({1})");
    }
}

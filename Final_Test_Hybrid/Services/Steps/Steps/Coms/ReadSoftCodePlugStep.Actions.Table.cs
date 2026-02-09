using Final_Test_Hybrid.Models.Errors;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.Coms;

public partial class ReadSoftCodePlugStep
{
    private static IReadOnlyList<SoftCodePlugAction> BuildActions()
    {
        return
        [
            BuildConnectionTypeAction(),
            BuildArticleAction(),
            BuildBoilerTypeAction(),
            BuildPumpTypeAction(),
            BuildPressureSensorTypeAction(),
            BuildGasRegulatorTypeAction(),
            BuildMaxChHeatOutputAction(),
            BuildMaxDhwHeatOutputAction(),
            BuildMinChHeatOutputAction(),
            BuildPumpModeAction(),
            BuildPumpPowerAction(),
            BuildGasTypeAction(),
            BuildCurrentOffsetAction(),
            BuildFlowCoefficientAction(),
            BuildMaxPumpAutoPowerAction(),
            BuildMinPumpAutoPowerAction(),
            BuildComfortHysteresisAction(),
            BuildMaxFlowTemperatureAction(),
            BuildItelmaArticleAction(),
            BuildProductionDateAction(),
            BuildSupplierCodeAction(),
            BuildCounterNumberAction(),
            BuildThermostatJumperAction()
        ];
    }

    private static VerifyStringAction CreateVerifyStringAction(
        int stepNo,
        string title,
        ushort startRegister,
        int registerCount,
        int maxLength,
        bool usesBoilerArticle,
        string? expectedRecipeKey,
        string resultName,
        ErrorDefinition mismatchError,
        string readLogMessage,
        string readErrorPrefix,
        string statusLogTemplate,
        string mismatchTemplate)
    {
        return new VerifyStringAction(
            StepNo: stepNo,
            Title: title,
            StartRegister: startRegister,
            RegisterCount: registerCount,
            MaxLength: maxLength,
            UsesBoilerArticle: usesBoilerArticle,
            ExpectedRecipeKey: expectedRecipeKey,
            ResultName: resultName,
            Unit: "",
            MismatchError: mismatchError,
            ReadLogMessage: readLogMessage,
            ReadErrorPrefix: readErrorPrefix,
            StatusLogTemplate: statusLogTemplate,
            MismatchTemplate: mismatchTemplate);
    }

    private static VerifyUInt16Action CreateVerifyUInt16Action(
        int stepNo,
        string title,
        ushort register,
        string expectedRecipeKey,
        string minRecipeKey,
        string maxRecipeKey,
        string resultName,
        string unit,
        ErrorDefinition mismatchError,
        string readLogMessage,
        string readErrorPrefix,
        string statusLogTemplate,
        string mismatchTemplate,
        Func<TestStepContext, bool>? shouldRun = null,
        string? skipLogMessage = null)
    {
        return new VerifyUInt16Action(
            StepNo: stepNo,
            Title: title,
            Register: register,
            ExpectedRecipeKey: expectedRecipeKey,
            MinRecipeKey: minRecipeKey,
            MaxRecipeKey: maxRecipeKey,
            ResultName: resultName,
            Unit: unit,
            MismatchError: mismatchError,
            ReadLogMessage: readLogMessage,
            ReadErrorPrefix: readErrorPrefix,
            StatusLogTemplate: statusLogTemplate,
            MismatchTemplate: mismatchTemplate,
            ShouldRun: shouldRun,
            SkipLogMessage: skipLogMessage);
    }

    private static VerifyFloatAction CreateVerifyFloatAction(
        int stepNo,
        string title,
        ushort startRegister,
        int registerCount,
        string expectedRecipeKey,
        string minRecipeKey,
        string maxRecipeKey,
        string resultName,
        string unit,
        string resultFormat,
        ErrorDefinition mismatchError,
        string readLogMessage,
        string readErrorPrefix,
        string statusLogTemplate,
        string mismatchTemplate,
        Func<TestStepContext, bool>? shouldRun = null,
        string? skipLogMessage = null)
    {
        return new VerifyFloatAction(
            StepNo: stepNo,
            Title: title,
            StartRegister: startRegister,
            RegisterCount: registerCount,
            ExpectedRecipeKey: expectedRecipeKey,
            MinRecipeKey: minRecipeKey,
            MaxRecipeKey: maxRecipeKey,
            ResultName: resultName,
            Unit: unit,
            ResultFormat: resultFormat,
            MismatchError: mismatchError,
            ReadLogMessage: readLogMessage,
            ReadErrorPrefix: readErrorPrefix,
            StatusLogTemplate: statusLogTemplate,
            MismatchTemplate: mismatchTemplate,
            ShouldRun: shouldRun,
            SkipLogMessage: skipLogMessage);
    }

    private static ReadOnlyStringAction CreateReadOnlyStringAction(
        int stepNo,
        string title,
        ushort startRegister,
        int registerCount,
        int maxLength,
        string resultName,
        string readLogMessage,
        string readErrorPrefix,
        string valueLogTemplate)
    {
        return new ReadOnlyStringAction(
            StepNo: stepNo,
            Title: title,
            StartRegister: startRegister,
            RegisterCount: registerCount,
            MaxLength: maxLength,
            ResultName: resultName,
            Unit: "",
            ReadLogMessage: readLogMessage,
            ReadErrorPrefix: readErrorPrefix,
            ValueLogTemplate: valueLogTemplate);
    }

    private static ReadOnlyUInt32Action CreateReadOnlyUInt32Action(
        int stepNo,
        string title,
        ushort startRegister,
        int registerCount,
        string resultName,
        string min,
        string max,
        string readLogMessage,
        string readErrorPrefix,
        string valueLogTemplate)
    {
        return new ReadOnlyUInt32Action(
            StepNo: stepNo,
            Title: title,
            StartRegister: startRegister,
            RegisterCount: registerCount,
            ResultName: resultName,
            Unit: "",
            Min: min,
            Max: max,
            ReadLogMessage: readLogMessage,
            ReadErrorPrefix: readErrorPrefix,
            ValueLogTemplate: valueLogTemplate);
    }
}

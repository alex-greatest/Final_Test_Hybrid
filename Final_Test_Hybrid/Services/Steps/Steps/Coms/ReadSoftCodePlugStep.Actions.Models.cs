using Final_Test_Hybrid.Models.Errors;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.Coms;

public partial class ReadSoftCodePlugStep
{
    private abstract record SoftCodePlugAction(
        int StepNo,
        string Title,
        Func<TestStepContext, bool>? ShouldRun = null,
        string? SkipLogMessage = null)
    {
        public virtual IReadOnlyList<string> RecipeKeys => [];
        public virtual string? ResultParameterName => null;
    }

    private sealed record VerifyConnectionType1054Action(
        int StepNo,
        string Title,
        ushort Register,
        string ExpectedRecipeKey,
        string ReadLogMessage,
        string ReadErrorPrefix,
        string MismatchMessage,
        ErrorDefinition MismatchError) : SoftCodePlugAction(StepNo, Title)
    {
        public override IReadOnlyList<string> RecipeKeys => [ExpectedRecipeKey];
    }

    private sealed record VerifyStringAction(
        int StepNo,
        string Title,
        ushort StartRegister,
        int RegisterCount,
        int MaxLength,
        bool UsesBoilerArticle,
        string? ExpectedRecipeKey,
        string ResultName,
        string Unit,
        ErrorDefinition MismatchError,
        string ReadLogMessage,
        string ReadErrorPrefix,
        string StatusLogTemplate,
        string MismatchTemplate,
        Func<TestStepContext, bool>? ShouldRun = null,
        string? SkipLogMessage = null) : SoftCodePlugAction(StepNo, Title, ShouldRun, SkipLogMessage)
    {
        public override IReadOnlyList<string> RecipeKeys =>
            UsesBoilerArticle || string.IsNullOrWhiteSpace(ExpectedRecipeKey) ? [] : [ExpectedRecipeKey];

        public override string? ResultParameterName => ResultName;
    }

    private sealed record VerifyUInt16Action(
        int StepNo,
        string Title,
        ushort Register,
        string ExpectedRecipeKey,
        string MinRecipeKey,
        string MaxRecipeKey,
        string ResultName,
        string Unit,
        ErrorDefinition MismatchError,
        string ReadLogMessage,
        string ReadErrorPrefix,
        string StatusLogTemplate,
        string MismatchTemplate,
        Func<TestStepContext, bool>? ShouldRun = null,
        string? SkipLogMessage = null) : SoftCodePlugAction(StepNo, Title, ShouldRun, SkipLogMessage)
    {
        public override IReadOnlyList<string> RecipeKeys => [ExpectedRecipeKey, MinRecipeKey, MaxRecipeKey];
        public override string? ResultParameterName => ResultName;
    }

    private sealed record VerifyFloatAction(
        int StepNo,
        string Title,
        ushort StartRegister,
        int RegisterCount,
        string ExpectedRecipeKey,
        string MinRecipeKey,
        string MaxRecipeKey,
        string ResultName,
        string Unit,
        string ResultFormat,
        ErrorDefinition MismatchError,
        string ReadLogMessage,
        string ReadErrorPrefix,
        string StatusLogTemplate,
        string MismatchTemplate,
        Func<TestStepContext, bool>? ShouldRun = null,
        string? SkipLogMessage = null) : SoftCodePlugAction(StepNo, Title, ShouldRun, SkipLogMessage)
    {
        public override IReadOnlyList<string> RecipeKeys => [ExpectedRecipeKey, MinRecipeKey, MaxRecipeKey];
        public override string? ResultParameterName => ResultName;
    }

    private sealed record ReadOnlyStringAction(
        int StepNo,
        string Title,
        ushort StartRegister,
        int RegisterCount,
        int MaxLength,
        string ResultName,
        string Unit,
        string ReadLogMessage,
        string ReadErrorPrefix,
        string ValueLogTemplate) : SoftCodePlugAction(StepNo, Title)
    {
        public override string? ResultParameterName => ResultName;
    }

    private sealed record ReadOnlyUInt32Action(
        int StepNo,
        string Title,
        ushort StartRegister,
        int RegisterCount,
        string ResultName,
        string Unit,
        string Min,
        string Max,
        string ReadLogMessage,
        string ReadErrorPrefix,
        string ValueLogTemplate) : SoftCodePlugAction(StepNo, Title)
    {
        public override string? ResultParameterName => ResultName;
    }

    private sealed record ThermostatJumperCheckAction(
        int StepNo,
        string Title,
        ushort Register,
        string ReadLogMessage,
        string ReadErrorPrefix,
        string StatusLogTemplate,
        string MissingMessage,
        ErrorDefinition MissingError) : SoftCodePlugAction(StepNo, Title);
}

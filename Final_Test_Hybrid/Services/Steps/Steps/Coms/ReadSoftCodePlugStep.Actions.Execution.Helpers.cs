using System.Globalization;
using Final_Test_Hybrid.Models.Errors;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.Coms;

public partial class ReadSoftCodePlugStep
{
    private Task<TestStepResult> ExecuteActionAsync(SoftCodePlugAction action, TestStepContext context, CancellationToken ct)
    {
        return action switch
        {
            VerifyConnectionType1054Action verifyConnection => ExecuteVerifyConnectionTypeActionAsync(verifyConnection, context, ct),
            VerifyStringAction verifyString => ExecuteVerifyStringActionAsync(verifyString, context, ct),
            VerifyUInt16Action verifyUInt16 => ExecuteVerifyUInt16ActionAsync(verifyUInt16, context, ct),
            VerifyFloatAction verifyFloat => ExecuteVerifyFloatActionAsync(verifyFloat, context, ct),
            ReadOnlyStringAction readOnlyString => ExecuteReadOnlyStringActionAsync(readOnlyString, context, ct),
            ReadOnlyUInt32Action readOnlyUInt32 => ExecuteReadOnlyUInt32ActionAsync(readOnlyUInt32, context, ct),
            ThermostatJumperCheckAction thermostat => ExecuteThermostatJumperCheckActionAsync(thermostat, context, ct),
            _ => Task.FromResult(CreateConfigurationFailure($"Неизвестный тип действия: {action.GetType().Name}"))
        };
    }

    private (bool Success, string Value, TestStepResult Result) GetExpectedStringValue(VerifyStringAction action, TestStepContext context)
    {
        if (!action.UsesBoilerArticle)
        {
            var value = context.RecipeProvider.GetStringValue(action.ExpectedRecipeKey!) ?? "";
            return (true, value, TestStepResult.Pass());
        }

        if (!string.IsNullOrEmpty(boilerState.Article))
        {
            return (true, boilerState.Article, TestStepResult.Pass());
        }

        const string message = "Артикул не задан в BoilerState";
        logger.LogError(message);
        var result = TestStepResult.Fail(message, errors: [ErrorDefinitions.EcuArticleMismatch]);
        return (false, string.Empty, result);
    }

    private bool ShouldSkipAction(SoftCodePlugAction action, TestStepContext context)
    {
        if (action.ShouldRun == null || action.ShouldRun(context))
        {
            return false;
        }

        logger.LogInformation(action.SkipLogMessage!);
        return true;
    }

    private TestStepResult CreateReadFailureResult<T>(
        Final_Test_Hybrid.Services.Diagnostic.Models.DiagnosticReadResult<T> result,
        string operation,
        string prefix)
    {
        var message = ComsStepFailureHelper.BuildReadMessage(result, operation, CreateReadFailureMessage(prefix, result.Error));
        logger.LogError(message);
        return TestStepResult.Fail(message);
    }

    private TestStepResult CreateMismatchFailureResult(string template, ErrorDefinition errorDefinition, params object?[] args)
    {
        var message = CreateFormattedMessage(template, args);
        logger.LogError(message);
        return TestStepResult.Fail(message, errors: [errorDefinition]);
    }

    private static string CreateReadFailureMessage(string prefix, string? error)
    {
        return $"{prefix}{error}";
    }

    private static string CreateFormattedMessage(string template, params object?[] args)
    {
        return string.Format(CultureInfo.InvariantCulture, template, args);
    }

    private static string ToStatusText(bool success)
    {
        return success ? "OK" : "NOK";
    }
}

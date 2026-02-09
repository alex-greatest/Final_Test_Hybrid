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

    private async Task<TestStepResult> ExecuteVerifyConnectionTypeActionAsync(
        VerifyConnectionType1054Action action,
        TestStepContext context,
        CancellationToken ct)
    {
        logger.LogInformation(action.ReadLogMessage);

        var expected = context.RecipeProvider.GetValue<ushort>(action.ExpectedRecipeKey)!.Value;
        var address = (ushort)(action.Register - _settings.BaseAddressOffset);
        var read = await context.DiagReader.ReadUInt16Async(address, ct);

        if (!read.Success)
        {
            return CreateReadFailureResult(action.ReadErrorPrefix, read.Error);
        }

        if (read.Value == expected)
        {
            return TestStepResult.Pass();
        }

        logger.LogError(action.MismatchMessage);
        return TestStepResult.Fail(action.MismatchMessage, errors: [action.MismatchError]);
    }

    private async Task<TestStepResult> ExecuteVerifyStringActionAsync(
        VerifyStringAction action,
        TestStepContext context,
        CancellationToken ct)
    {
        if (ShouldSkipAction(action, context))
        {
            return TestStepResult.Pass();
        }

        var expectedValueResult = GetExpectedStringValue(action, context);
        if (!expectedValueResult.Success)
        {
            return expectedValueResult.Result;
        }

        logger.LogInformation(action.ReadLogMessage);

        var address = (ushort)(action.StartRegister - _settings.BaseAddressOffset);
        var read = await context.DiagReader.ReadStringAsync(address, action.MaxLength, ct);

        if (!read.Success)
        {
            return CreateReadFailureResult(action.ReadErrorPrefix, read.Error);
        }

        var actualValue = read.Value;
        var expectedValue = expectedValueResult.Value;
        var isMatch = actualValue == expectedValue;

        testResultsService.Add(
            parameterName: action.ResultName,
            value: actualValue ?? "",
            min: "",
            max: "",
            status: isMatch ? 1 : 0,
            isRanged: true,
            unit: action.Unit);

        var statusMessage = CreateFormattedMessage(action.StatusLogTemplate, actualValue, expectedValue, ToStatusText(isMatch));
        logger.LogInformation(statusMessage);

        return isMatch
            ? TestStepResult.Pass()
            : CreateMismatchFailureResult(action.MismatchTemplate, action.MismatchError, actualValue, expectedValue);
    }

    private async Task<TestStepResult> ExecuteVerifyUInt16ActionAsync(
        VerifyUInt16Action action,
        TestStepContext context,
        CancellationToken ct)
    {
        if (ShouldSkipAction(action, context))
        {
            return TestStepResult.Pass();
        }

        var expectedValue = context.RecipeProvider.GetValue<ushort>(action.ExpectedRecipeKey)!.Value;
        var minValue = context.RecipeProvider.GetValue<ushort>(action.MinRecipeKey)!.Value;
        var maxValue = context.RecipeProvider.GetValue<ushort>(action.MaxRecipeKey)!.Value;

        logger.LogInformation(action.ReadLogMessage);

        var address = (ushort)(action.Register - _settings.BaseAddressOffset);
        var read = await context.DiagReader.ReadUInt16Async(address, ct);

        if (!read.Success)
        {
            return CreateReadFailureResult(action.ReadErrorPrefix, read.Error);
        }

        var actualValue = read.Value;
        var isMatch = actualValue == expectedValue;

        testResultsService.Add(
            parameterName: action.ResultName,
            value: actualValue.ToString(),
            min: minValue.ToString(),
            max: maxValue.ToString(),
            status: isMatch ? 1 : 0,
            isRanged: true,
            unit: action.Unit);

        var statusMessage = CreateFormattedMessage(action.StatusLogTemplate, actualValue, expectedValue, ToStatusText(isMatch));
        logger.LogInformation(statusMessage);

        return isMatch
            ? TestStepResult.Pass()
            : CreateMismatchFailureResult(action.MismatchTemplate, action.MismatchError, actualValue, expectedValue);
    }

    private async Task<TestStepResult> ExecuteVerifyFloatActionAsync(
        VerifyFloatAction action,
        TestStepContext context,
        CancellationToken ct)
    {
        if (ShouldSkipAction(action, context))
        {
            return TestStepResult.Pass();
        }

        var expectedValue = context.RecipeProvider.GetValue<float>(action.ExpectedRecipeKey)!.Value;
        var minValue = context.RecipeProvider.GetValue<float>(action.MinRecipeKey)!.Value;
        var maxValue = context.RecipeProvider.GetValue<float>(action.MaxRecipeKey)!.Value;

        logger.LogInformation(action.ReadLogMessage);

        var address = (ushort)(action.StartRegister - _settings.BaseAddressOffset);
        var read = await context.DiagReader.ReadFloatAsync(address, ct);

        if (!read.Success)
        {
            return CreateReadFailureResult(action.ReadErrorPrefix, read.Error);
        }

        var actualValue = read.Value;
        var isMatch = actualValue == expectedValue;

        testResultsService.Add(
            parameterName: action.ResultName,
            value: actualValue.ToString(action.ResultFormat),
            min: minValue.ToString(action.ResultFormat),
            max: maxValue.ToString(action.ResultFormat),
            status: isMatch ? 1 : 0,
            isRanged: true,
            unit: action.Unit);

        var statusMessage = CreateFormattedMessage(action.StatusLogTemplate, actualValue, expectedValue, ToStatusText(isMatch));
        logger.LogInformation(statusMessage);

        return isMatch
            ? TestStepResult.Pass()
            : CreateMismatchFailureResult(action.MismatchTemplate, action.MismatchError, actualValue, expectedValue);
    }

    private async Task<TestStepResult> ExecuteReadOnlyStringActionAsync(
        ReadOnlyStringAction action,
        TestStepContext context,
        CancellationToken ct)
    {
        if (ShouldSkipAction(action, context))
        {
            return TestStepResult.Pass();
        }

        logger.LogInformation(action.ReadLogMessage);

        var address = (ushort)(action.StartRegister - _settings.BaseAddressOffset);
        var read = await context.DiagReader.ReadStringAsync(address, action.MaxLength, ct);

        if (!read.Success)
        {
            return CreateReadFailureResult(action.ReadErrorPrefix, read.Error);
        }

        testResultsService.Add(
            parameterName: action.ResultName,
            value: read.Value ?? "",
            min: "",
            max: "",
            status: 1,
            isRanged: true,
            unit: action.Unit);

        var valueMessage = CreateFormattedMessage(action.ValueLogTemplate, read.Value);
        logger.LogInformation(valueMessage);
        return TestStepResult.Pass();
    }

    private async Task<TestStepResult> ExecuteReadOnlyUInt32ActionAsync(
        ReadOnlyUInt32Action action,
        TestStepContext context,
        CancellationToken ct)
    {
        if (ShouldSkipAction(action, context))
        {
            return TestStepResult.Pass();
        }

        logger.LogInformation(action.ReadLogMessage);

        var address = (ushort)(action.StartRegister - _settings.BaseAddressOffset);
        var read = await context.DiagReader.ReadUInt32Async(address, ct);

        if (!read.Success)
        {
            return CreateReadFailureResult(action.ReadErrorPrefix, read.Error);
        }

        testResultsService.Add(
            parameterName: action.ResultName,
            value: read.Value.ToString(),
            min: "",
            max: "",
            status: 1,
            isRanged: true,
            unit: action.Unit);

        var valueMessage = CreateFormattedMessage(action.ValueLogTemplate, read.Value);
        logger.LogInformation(valueMessage);
        return TestStepResult.Pass();
    }

    private async Task<TestStepResult> ExecuteThermostatJumperCheckActionAsync(
        ThermostatJumperCheckAction action,
        TestStepContext context,
        CancellationToken ct)
    {
        if (ShouldSkipAction(action, context))
        {
            return TestStepResult.Pass();
        }

        logger.LogInformation(action.ReadLogMessage);

        var address = (ushort)(action.Register - _settings.BaseAddressOffset);
        var read = await context.DiagReader.ReadUInt16Async(address, ct);

        if (!read.Success)
        {
            return CreateReadFailureResult(action.ReadErrorPrefix, read.Error);
        }

        var isMissing = read.Value == 0;
        var statusMessage = CreateFormattedMessage(action.StatusLogTemplate, read.Value, isMissing ? "Open" : "Closed");
        logger.LogInformation(statusMessage);

        if (!isMissing)
        {
            return TestStepResult.Pass();
        }

        logger.LogError(action.MissingMessage);
        return TestStepResult.Fail(action.MissingMessage, errors: [action.MissingError]);
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

    private TestStepResult CreateReadFailureResult(string prefix, string? error)
    {
        var message = CreateReadFailureMessage(prefix, error);
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

    private TestStepResult SaveSoftCodePlugResult()
    {
        var article = boilerState.Article;
        if (string.IsNullOrWhiteSpace(article))
        {
            const string message = "Артикул котла не задан в BoilerState";
            logger.LogError(message);
            return TestStepResult.Fail(message, errors: [ErrorDefinitions.EcuArticleMismatch]);
        }

        testResultsService.Add(
            parameterName: SoftCodePlugResultName,
            value: article,
            min: "",
            max: "",
            status: 1,
            isRanged: false,
            unit: "");

        logger.LogInformation("Soft_Code_Plug сохранён: {Article}", article);
        return TestStepResult.Pass();
    }
}

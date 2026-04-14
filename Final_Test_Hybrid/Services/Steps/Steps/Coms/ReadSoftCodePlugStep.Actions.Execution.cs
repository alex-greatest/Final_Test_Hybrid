using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.Coms;

public partial class ReadSoftCodePlugStep
{
    private async Task<TestStepResult> ExecuteVerifyConnectionTypeActionAsync(
        VerifyConnectionType1054Action action,
        TestStepContext context,
        CancellationToken ct)
    {
        logger.LogInformation(action.ReadLogMessage);

        var expected = context.RecipeProvider.GetValue<ushort>(action.ExpectedRecipeKey)!.Value;
        var address = (ushort)(action.Register - _settings.BaseAddressOffset);
        var read = await context.PacedDiagReader.ReadUInt16Async(address, ct);

        if (!read.Success)
        {
            return CreateReadFailureResult(read, $"чтении регистра {action.Register}", action.ReadErrorPrefix);
        }

        var actualValue = read.Value;
        var isMatch = actualValue == expected;

        testResultsService.Add(
            parameterName: action.ResultName,
            value: actualValue.ToString(),
            min: "",
            max: "",
            status: isMatch ? 1 : 2,
            isRanged: false,
            unit: "",
            test: Name);

        if (isMatch)
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
        var read = await context.PacedDiagReader.ReadStringAsync(address, action.MaxLength, ct);

        if (!read.Success)
        {
            return CreateReadFailureResult(read, $"чтении регистров {action.StartRegister}-{action.StartRegister + ((action.MaxLength + 1) / 2) - 1}", action.ReadErrorPrefix);
        }

        var actualValue = read.Value;
        var expectedValue = expectedValueResult.Value;
        var isMatch = actualValue == expectedValue;

        testResultsService.Add(
            parameterName: action.ResultName,
            value: actualValue ?? "",
            min: "",
            max: "",
            status: isMatch ? 1 : 2,
            isRanged: false,
            unit: action.Unit,
            test: Name);

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
        var read = await context.PacedDiagReader.ReadUInt16Async(address, ct);

        if (!read.Success)
        {
            return CreateReadFailureResult(read, $"чтении регистра {action.Register}", action.ReadErrorPrefix);
        }

        var actualValue = read.Value;
        var isMatch = actualValue == expectedValue;

        testResultsService.Add(
            parameterName: action.ResultName,
            value: actualValue.ToString(),
            min: minValue.ToString(),
            max: maxValue.ToString(),
            status: isMatch ? 1 : 2,
            isRanged: true,
            unit: action.Unit,
            test: Name);

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
        var read = await context.PacedDiagReader.ReadFloatAsync(address, ct);

        if (!read.Success)
        {
            return CreateReadFailureResult(read, $"чтении регистров {action.StartRegister}-{action.StartRegister + 1}", action.ReadErrorPrefix);
        }

        var actualValue = read.Value;
        var isMatch = actualValue == expectedValue;

        testResultsService.Add(
            parameterName: action.ResultName,
            value: actualValue.ToString(action.ResultFormat),
            min: minValue.ToString(action.ResultFormat),
            max: maxValue.ToString(action.ResultFormat),
            status: isMatch ? 1 : 2,
            isRanged: true,
            unit: action.Unit,
            test: Name);

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
        var read = await context.PacedDiagReader.ReadStringAsync(address, action.MaxLength, ct);

        if (!read.Success)
        {
            return CreateReadFailureResult(read, $"чтении регистров {action.StartRegister}-{action.StartRegister + ((action.MaxLength + 1) / 2) - 1}", action.ReadErrorPrefix);
        }

        testResultsService.Add(
            parameterName: action.ResultName,
            value: read.Value ?? "",
            min: "",
            max: "",
            status: 1,
            isRanged: false,
            unit: action.Unit,
            test: Name);

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
        var read = await context.PacedDiagReader.ReadUInt32Async(address, ct);

        if (!read.Success)
        {
            return CreateReadFailureResult(read, $"чтении регистров {action.StartRegister}-{action.StartRegister + 1}", action.ReadErrorPrefix);
        }

        testResultsService.Add(
            parameterName: action.ResultName,
            value: read.Value.ToString(),
            min: action.Min,
            max: action.Max,
            status: 1,
            isRanged: true,
            unit: action.Unit,
            test: Name);

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
        var read = await context.PacedDiagReader.ReadUInt16Async(address, ct);

        if (!read.Success)
        {
            return CreateReadFailureResult(read, $"чтении регистра {action.Register}", action.ReadErrorPrefix);
        }

        var isMissing = read.Value == 0;
        var statusMessage = CreateFormattedMessage(action.StatusLogTemplate, read.Value, isMissing ? "Open" : "Closed");
        logger.LogInformation(statusMessage);

        testResultsService.Add(
            parameterName: action.ResultName,
            value: read.Value.ToString(),
            min: "",
            max: "",
            status: isMissing ? 2 : 1,
            isRanged: false,
            unit: "",
            test: Name);

        if (!isMissing)
        {
            return TestStepResult.Pass();
        }

        logger.LogError(action.MissingMessage);
        return TestStepResult.Fail(action.MissingMessage, errors: [action.MissingError]);
    }

}

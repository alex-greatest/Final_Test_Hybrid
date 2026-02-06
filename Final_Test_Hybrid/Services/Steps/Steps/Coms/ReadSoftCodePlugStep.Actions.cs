using Final_Test_Hybrid.Models.Errors;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.Coms;

public partial class ReadSoftCodePlugStep
{
    private TestStepResult? ValidateActions(IReadOnlyList<SoftCodePlugAction> actions)
    {
        var stepNumbersValidation = ValidateStepNumbers(actions);
        return stepNumbersValidation ?? actions.Select(ValidateSingleAction).OfType<TestStepResult>().FirstOrDefault();
    }

    private TestStepResult? ValidateStepNumbers(IReadOnlyList<SoftCodePlugAction> actions)
    {
        var uniqueValidation = ValidateUniqueStepNumbers(actions);
        return uniqueValidation ?? ValidateContinuousStepNumbers(actions);
    }

    private TestStepResult? ValidateUniqueStepNumbers(IReadOnlyList<SoftCodePlugAction> actions)
    {
        var usedNumbers = new HashSet<int>();
        return (from action in actions where !usedNumbers.Add(action.StepNo) select CreateConfigurationFailure($"Дублируется номер подшага: {action.StepNo}")).FirstOrDefault();
    }

    private TestStepResult? ValidateContinuousStepNumbers(IReadOnlyList<SoftCodePlugAction> actions)
    {
        var orderedNumbers = actions.Select(x => x.StepNo).OrderBy(x => x).ToArray();
        for (var index = 0; index < orderedNumbers.Length; index++)
        {
            var expected = index + 1;
            if (orderedNumbers[index] == expected)
            {
                continue;
            }

            return CreateConfigurationFailure($"Номера подшагов должны быть непрерывны 1..N. Ожидался {expected}, получен {orderedNumbers[index]}");
        }

        return null;
    }

    private TestStepResult? ValidateSingleAction(SoftCodePlugAction action)
    {
        var skipValidation = ValidateSkipCondition(action);
        if (skipValidation != null)
        {
            return skipValidation;
        }

        return action switch
        {
            VerifyConnectionType1054Action verifyConnection => ValidateVerifyConnectionAction(verifyConnection),
            VerifyStringAction verifyString => ValidateVerifyStringAction(verifyString),
            VerifyUInt16Action verifyUInt16 => ValidateVerifyUInt16Action(verifyUInt16),
            VerifyFloatAction verifyFloat => ValidateVerifyFloatAction(verifyFloat),
            ReadOnlyStringAction readOnlyString => ValidateReadOnlyStringAction(readOnlyString),
            ReadOnlyUInt32Action readOnlyUInt32 => ValidateReadOnlyUInt32Action(readOnlyUInt32),
            ThermostatJumperCheckAction thermostat => ValidateThermostatAction(thermostat),
            _ => CreateConfigurationFailure($"Неизвестный тип действия: {action.GetType().Name}")
        };
    }

    private TestStepResult? ValidateSkipCondition(SoftCodePlugAction action)
    {
        if (action.ShouldRun == null || !string.IsNullOrWhiteSpace(action.SkipLogMessage))
        {
            return null;
        }

        return CreateConfigurationFailure($"Для подшага {action.StepNo} ({action.Title}) задан ShouldRun без SkipLogMessage");
    }

    private TestStepResult? ValidateVerifyConnectionAction(VerifyConnectionType1054Action action)
    {
        if (!string.IsNullOrWhiteSpace(action.ExpectedRecipeKey))
        {
            return null;
        }

        return CreateConfigurationFailure($"Для подшага {action.StepNo} ({action.Title}) не задан ключ expected-рецепта");
    }

    private TestStepResult? ValidateVerifyStringAction(VerifyStringAction action)
    {
        var rangeValidation = ValidateStringRange(action.StepNo, action.Title, action.StartRegister, action.RegisterCount, action.MaxLength);
        if (rangeValidation != null)
        {
            return rangeValidation;
        }

        var recipeValidation = ValidateVerifyStringRecipeKey(action);
        return recipeValidation ?? ValidateResultName(action.StepNo, action.Title, action.ResultName);
    }

    private TestStepResult? ValidateVerifyUInt16Action(VerifyUInt16Action action)
    {
        var keyValidation = ValidateVerifyRecipeKeys(action.StepNo, action.Title, action.ExpectedRecipeKey, action.MinRecipeKey, action.MaxRecipeKey);
        return keyValidation ?? ValidateResultName(action.StepNo, action.Title, action.ResultName);
    }

    private TestStepResult? ValidateVerifyFloatAction(VerifyFloatAction action)
    {
        var rangeValidation = ValidateRegisterRange(action.StepNo, action.Title, action.StartRegister, action.RegisterCount);
        if (rangeValidation != null)
        {
            return rangeValidation;
        }

        var keyValidation = ValidateVerifyRecipeKeys(action.StepNo, action.Title, action.ExpectedRecipeKey, action.MinRecipeKey, action.MaxRecipeKey);
        return keyValidation ?? ValidateResultName(action.StepNo, action.Title, action.ResultName);
    }

    private TestStepResult? ValidateReadOnlyStringAction(ReadOnlyStringAction action)
    {
        var rangeValidation = ValidateStringRange(action.StepNo, action.Title, action.StartRegister, action.RegisterCount, action.MaxLength);
        return rangeValidation ?? ValidateResultName(action.StepNo, action.Title, action.ResultName);
    }

    private TestStepResult? ValidateReadOnlyUInt32Action(ReadOnlyUInt32Action action)
    {
        var rangeValidation = ValidateRegisterRange(action.StepNo, action.Title, action.StartRegister, action.RegisterCount);
        return rangeValidation ?? ValidateResultName(action.StepNo, action.Title, action.ResultName);
    }

    private TestStepResult? ValidateThermostatAction(ThermostatJumperCheckAction action)
    {
        return action.Register > 0
            ? null
            : CreateConfigurationFailure($"Для подшага {action.StepNo} ({action.Title}) задан некорректный регистр");
    }

    private TestStepResult? ValidateStringRange(int stepNo, string title, ushort startRegister, int registerCount, int maxLength)
    {
        var rangeValidation = ValidateRegisterRange(stepNo, title, startRegister, registerCount);
        if (rangeValidation != null)
        {
            return rangeValidation;
        }

        return maxLength > 0
            ? null
            : CreateConfigurationFailure($"Для подшага {stepNo} ({title}) maxLength должен быть > 0");
    }

    private TestStepResult? ValidateRegisterRange(int stepNo, string title, ushort startRegister, int registerCount)
    {
        if (startRegister > 0 && registerCount > 0)
        {
            return null;
        }

        return CreateConfigurationFailure($"Для подшага {stepNo} ({title}) задан некорректный диапазон регистров");
    }

    private TestStepResult? ValidateVerifyStringRecipeKey(VerifyStringAction action)
    {
        if (action.UsesBoilerArticle || !string.IsNullOrWhiteSpace(action.ExpectedRecipeKey))
        {
            return null;
        }

        return CreateConfigurationFailure($"Для подшага {action.StepNo} ({action.Title}) не задан ключ expected-рецепта");
    }

    private TestStepResult? ValidateVerifyRecipeKeys(int stepNo, string title, string expectedKey, string minKey, string maxKey)
    {
        if (!string.IsNullOrWhiteSpace(expectedKey) &&
            !string.IsNullOrWhiteSpace(minKey) &&
            !string.IsNullOrWhiteSpace(maxKey))
        {
            return null;
        }

        return CreateConfigurationFailure($"Для подшага {stepNo} ({title}) не заданы обязательные recipe keys (expected/min/max)");
    }

    private TestStepResult? ValidateResultName(int stepNo, string title, string resultName)
    {
        return !string.IsNullOrWhiteSpace(resultName) ? null : CreateConfigurationFailure($"Для подшага {stepNo} ({title}) не задан ResultName");
    }

    private TestStepResult CreateConfigurationFailure(string details)
    {
        logger.LogError("Ошибка конфигурации шага {StepName}: {Details}", Name, details);
        return TestStepResult.Fail($"Ошибка конфигурации шага {Name}.");
    }
}

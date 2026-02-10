using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Results;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Completion;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Limits;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.Gas;

/// <summary>
/// Тестовый шаг регулировки давления и расхода газа на минимальной мощности.
/// Отображает два слайдера для визуализации параметров газа.
/// </summary>
public class SetGasAndPBurnerMinLevelsStep(
    DualLogger<SetGasAndPBurnerMinLevelsStep> logger,
    ITestResultsService testResultsService)
    : ITestStep, IHasPlcBlockPath, IRequiresPlcSubscriptions, IRequiresRecipes, IProvideLimits
{
    private const string BlockPath = "DB_VI.Gas.Set_Gas_and_P_Burner_Min_Levels";
    private const string StartTag = "ns=3;s=\"DB_VI\".\"Gas\".\"Set_Gas_and_P_Burner_Min_Levels\".\"Start\"";
    private const string EndTag = "ns=3;s=\"DB_VI\".\"Gas\".\"Set_Gas_and_P_Burner_Min_Levels\".\"End\"";
    private const string ErrorTag = "ns=3;s=\"DB_VI\".\"Gas\".\"Set_Gas_and_P_Burner_Min_Levels\".\"Error\"";
    private const string Ready1Tag = "ns=3;s=\"DB_VI\".\"Gas\".\"Set_Gas_and_P_Burner_Min_Levels\".\"Ready_1\"";
    private const string Continue1Tag = "ns=3;s=\"DB_VI\".\"Gas\".\"Set_Gas_and_P_Burner_Min_Levels\".\"Continue_1\"";

    // Теги для слайдеров (привязка стрелки)
    private const string GasPgbTag = "ns=3;s=\"DB_Measure\".\"Sensor\".\"Gas_PGB\"";
    private const string GasPogTag = "ns=3;s=\"DB_Measure\".\"Sensor\".\"Gas_POG\"";

    // Теги для результатов (чтение из PLC)
    private const string BlrGasPressMinTag = "ns=3;s=\"DB_Parameter\".\"Blr\".\"Gas_Pres_Min\"";
    private const string GasFlowMinTag = "ns=3;s=\"DB_Parameter\".\"Gas\".\"Flow_Min\"";
    private const string BnrGasPressMinTag = "ns=3;s=\"DB_Parameter\".\"Bnr\".\"Gas_Pres_Min\"";

    // Рецепты для слайдера 1 - Давление горелки (BurnerPressMin)
    private const string BurnerPressMinSetValueRecipe = "ns=3;s=\"DB_Recipe\".\"Gas\".\"BurnerPressMin\".\"SetValue\"";
    private const string BurnerPressMinDownTolRecipe = "ns=3;s=\"DB_Recipe\".\"Gas\".\"BurnerPressMin\".\"DownTol\"";
    private const string BurnerPressMinUpTolRecipe = "ns=3;s=\"DB_Recipe\".\"Gas\".\"BurnerPressMin\".\"UpTol\"";

    // Рецепты для слайдера 2 - Расход газа (FlowMin)
    private const string FlowMinSetValueRecipe = "ns=3;s=\"DB_Recipe\".\"Gas\".\"FlowMin\".\"SetValue\"";
    private const string FlowMinDownTolRecipe = "ns=3;s=\"DB_Recipe\".\"Gas\".\"FlowMin\".\"DownTol\"";
    private const string FlowMinUpTolRecipe = "ns=3;s=\"DB_Recipe\".\"Gas\".\"FlowMin\".\"UpTol\"";

    // Рецепты для результата Blr_Gas_Pres_Min (SuppPressMin)
    private const string SuppPressMinSetValueRecipe = "ns=3;s=\"DB_Recipe\".\"Gas\".\"SuppPressMin\".\"SetValue\"";
    private const string SuppPressMinDownTolRecipe = "ns=3;s=\"DB_Recipe\".\"Gas\".\"SuppPressMin\".\"DownTol\"";
    private const string SuppPressMinUpTolRecipe = "ns=3;s=\"DB_Recipe\".\"Gas\".\"SuppPressMin\".\"UpTol\"";

    // Индексы слайдеров (используем 0 и 1 для одновременного показа двух слайдеров)
    private const int PressureSliderIndex = 0;
    private const int FlowSliderIndex = 1;

    public string Id => "gas-set-gas-and-p-burner-min-levels";
    public string Name => "Gas/Set_Gas_and_P_Burner_Min_Levels";
    public string Description => "Регулировка давления и расхода газа на минимальной мощности";
    public string PlcBlockPath => BlockPath;

    public IReadOnlyList<string> RequiredPlcTags =>
    [
        StartTag, EndTag, ErrorTag, Ready1Tag,
        GasPgbTag, GasPogTag,
        BlrGasPressMinTag, GasFlowMinTag, BnrGasPressMinTag
    ];

    public IReadOnlyList<string> RequiredRecipeAddresses =>
    [
        BurnerPressMinSetValueRecipe, BurnerPressMinDownTolRecipe, BurnerPressMinUpTolRecipe,
        FlowMinSetValueRecipe, FlowMinDownTolRecipe, FlowMinUpTolRecipe,
        SuppPressMinSetValueRecipe, SuppPressMinDownTolRecipe, SuppPressMinUpTolRecipe
    ];

    /// <summary>
    /// Возвращает пределы для отображения в гриде.
    /// Показывает три параметра: Pвх (SuppPressMin), Pгор (BurnerPressMin), Q (FlowMin).
    /// </summary>
    /// <param name="context">Контекст с доступом к рецептам.</param>
    /// <returns>Строка с пределами или null, если рецепты не загружены.</returns>
    public string? GetLimits(LimitsContext context)
    {
        // SuppPressMin — для Blr_Gas_Pres_Min (давление на входе)
        var suppSetValue = context.RecipeProvider.GetValue<float>(SuppPressMinSetValueRecipe);
        var suppDownTol = context.RecipeProvider.GetValue<float>(SuppPressMinDownTolRecipe);
        var suppUpTol = context.RecipeProvider.GetValue<float>(SuppPressMinUpTolRecipe);

        // BurnerPressMin — для Bnr_Gas_Pres_Min (давление горелки)
        var burnerSetValue = context.RecipeProvider.GetValue<float>(BurnerPressMinSetValueRecipe);
        var burnerDownTol = context.RecipeProvider.GetValue<float>(BurnerPressMinDownTolRecipe);
        var burnerUpTol = context.RecipeProvider.GetValue<float>(BurnerPressMinUpTolRecipe);

        // FlowMin — для Gas_Flow_Min (расход газа)
        var flowSetValue = context.RecipeProvider.GetValue<float>(FlowMinSetValueRecipe);
        var flowDownTol = context.RecipeProvider.GetValue<float>(FlowMinDownTolRecipe);
        var flowUpTol = context.RecipeProvider.GetValue<float>(FlowMinUpTolRecipe);

        if (suppSetValue == null || burnerSetValue == null || flowSetValue == null)
        {
            return null;
        }

        var suppMin = suppSetValue.Value - (suppDownTol ?? 0);
        var suppMax = suppSetValue.Value + (suppUpTol ?? 0);
        var burnerMin = burnerSetValue.Value - (burnerDownTol ?? 0);
        var burnerMax = burnerSetValue.Value + (burnerUpTol ?? 0);
        var flowMinVal = flowSetValue.Value - (flowDownTol ?? 0);
        var flowMaxVal = flowSetValue.Value + (flowUpTol ?? 0);

        return $"PAG:[{suppMin:F1}..{suppMax:F1}]; PGB:[{burnerMin:F1}..{burnerMax:F1}]; POG:[{flowMinVal:F1}..{flowMaxVal:F1}]";
    }

    /// <summary>
    /// Выполняет шаг регулировки параметров газа на минимальной мощности.
    /// </summary>
    /// <param name="context">Контекст выполнения шага с доступом к OPC-UA и UI.</param>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат выполнения шага (Pass/Fail).</returns>
    /// <exception cref="OperationCanceledException">При отмене операции через ct.</exception>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Запуск настройки параметров газа в минимальном режиме");

        // Удаляем старые результаты для корректного Retry
        testResultsService.Remove("Blr_Gas_Pres_Min");
        testResultsService.Remove("Gas_Flow_Min");
        testResultsService.Remove("Bnr_Gas_Pres_Min");

        var pressureConfig = CreatePressureSliderConfig(context);
        var flowConfig = CreateFlowSliderConfig(context);

        if (pressureConfig == null || flowConfig == null)
        {
            return TestStepResult.Fail("Не удалось загрузить рецепты для слайдеров");
        }

        try
        {
            // Показываем слайдеры внутри try для гарантированного cleanup
            context.RangeSliderUiState.SetTitle("Настройка параметров газа в минимальном режиме");
            await context.RangeSliderUiState.ShowAsync(PressureSliderIndex, pressureConfig, ct);
            await context.RangeSliderUiState.ShowAsync(FlowSliderIndex, flowConfig, ct);

            var writeResult = await context.OpcUa.WriteAsync(StartTag, true, ct);
            if (writeResult.Error != null)
            {
                return TestStepResult.Fail($"Ошибка записи Start: {writeResult.Error}");
            }

            return await WaitForCompletionAsync(context, ct);
        }
        finally
        {
            await HideSlidersAsync(context);
            context.RangeSliderUiState.SetTitle(null);
            // Start НЕ сбрасываем здесь — координатор сделает через ResetBlockStartAsync при ошибке
        }
    }

    /// <summary>
    /// Создаёт конфигурацию слайдера давления горелки.
    /// </summary>
    private RangeSliderConfig? CreatePressureSliderConfig(TestStepContext context)
    {
        var setValue = context.RecipeProvider.GetValue<float>(BurnerPressMinSetValueRecipe);
        var downTol = context.RecipeProvider.GetValue<float>(BurnerPressMinDownTolRecipe);
        var upTol = context.RecipeProvider.GetValue<float>(BurnerPressMinUpTolRecipe);

        if (setValue == null)
        {
            return null;
        }

        var greenStart = setValue.Value - (downTol ?? 0);
        var greenEnd = setValue.Value + (upTol ?? 0);

        return new RangeSliderConfig(
            Label: "Давление горелки",
            Unit: "мбар",
            ValueTag: GasPgbTag,
            GreenZoneStart: greenStart,
            GreenZoneEnd: greenEnd,
            MinValue: greenStart - 0.5,
            MaxValue: greenEnd + 0.5,
            Step: 0.1);
    }

    /// <summary>
    /// Создаёт конфигурацию слайдера расхода газа.
    /// </summary>
    private RangeSliderConfig? CreateFlowSliderConfig(TestStepContext context)
    {
        var setValue = context.RecipeProvider.GetValue<float>(FlowMinSetValueRecipe);
        var downTol = context.RecipeProvider.GetValue<float>(FlowMinDownTolRecipe);
        var upTol = context.RecipeProvider.GetValue<float>(FlowMinUpTolRecipe);

        if (setValue == null)
        {
            return null;
        }

        var greenStart = setValue.Value - (downTol ?? 0);
        var greenEnd = setValue.Value + (upTol ?? 0);

        return new RangeSliderConfig(
            Label: "Расход газа",
            Unit: "л/мин",
            ValueTag: GasPogTag,
            GreenZoneStart: greenStart,
            GreenZoneEnd: greenEnd,
            MinValue: greenStart - 4,
            MaxValue: greenEnd + 4,
            Step: 1);
    }

    /// <summary>
    /// Ожидает завершения операции настройки параметров газа.
    /// </summary>
    private async Task<TestStepResult> WaitForCompletionAsync(TestStepContext context, CancellationToken ct)
    {
        while (true)
        {
            var waitResult = await context.TagWaiter.WaitAnyAsync(
                context.TagWaiter.CreateWaitGroup<CompletionResult>()
                    .WaitForTrue(EndTag, () => CompletionResult.Success, "End")
                    .WaitForTrue(ErrorTag, () => CompletionResult.Error, "Error")
                    .WaitForTrue(Ready1Tag, () => CompletionResult.Ready1, "Ready_1"),
                ct);

            switch (waitResult.Result)
            {
                case CompletionResult.Success:
                    return await HandleCompletionAsync(context, isSuccess: true, ct);
                case CompletionResult.Error:
                    return await HandleCompletionAsync(context, isSuccess: false, ct);
                case CompletionResult.Ready1:
                    await context.OpcUa.WriteAsync(Continue1Tag, true, ct);
                    continue;
                default:
                    return TestStepResult.Fail("Неизвестный результат");
            }
        }
    }

    /// <summary>
    /// Обрабатывает завершение: чтение значений из PLC и сохранение результатов.
    /// </summary>
    private async Task<TestStepResult> HandleCompletionAsync(
        TestStepContext context,
        bool isSuccess,
        CancellationToken ct)
    {
        // Читаем результаты из PLC
        var (_, blrGasPress, blrError) = await context.OpcUa.ReadAsync<float>(BlrGasPressMinTag, ct);
        if (blrError != null)
        {
            return TestStepResult.Fail($"Ошибка чтения Blr.Gas_Pres_Min: {blrError}");
        }

        var (_, gasFlow, flowError) = await context.OpcUa.ReadAsync<float>(GasFlowMinTag, ct);
        if (flowError != null)
        {
            return TestStepResult.Fail($"Ошибка чтения Gas.Flow_Min: {flowError}");
        }

        var (_, bnrGasPress, bnrError) = await context.OpcUa.ReadAsync<float>(BnrGasPressMinTag, ct);
        if (bnrError != null)
        {
            return TestStepResult.Fail($"Ошибка чтения Bnr.Gas_Pres_Min: {bnrError}");
        }

        // Сохраняем результаты
        var status = isSuccess ? 1 : 2;

        SaveBlrGasPressMinResult(context, blrGasPress, status);
        SaveGasFlowMinResult(context, gasFlow, status);
        SaveBnrGasPressMinResult(context, bnrGasPress, status);

        logger.LogInformation(
            "Результаты: Blr_Gas_Pres_Min={BlrPress:F3}, Gas_Flow_Min={Flow:F3}, Bnr_Gas_Pres_Min={BnrPress:F3}, статус={Status}",
            blrGasPress, gasFlow, bnrGasPress, isSuccess ? "OK" : "NOK");

        if (isSuccess)
        {
            // Сбрасываем Start только при успехе — при ошибке координатор сделает через ResetBlockStartAsync
            await context.OpcUa.WriteAsync(StartTag, false, ct);
            return TestStepResult.Pass($"PAG:{blrGasPress:F2}; PGB:{bnrGasPress:F2}; POG:{gasFlow:F2}");
        }

        return TestStepResult.Fail($"PAG:{blrGasPress:F2}; PGB:{bnrGasPress:F2}; POG:{gasFlow:F2}");
    }

    /// <summary>
    /// Сохраняет результат Blr_Gas_Pres_Min (с пределами SuppPressMin).
    /// </summary>
    private void SaveBlrGasPressMinResult(TestStepContext context, float value, int status)
    {
        var setValue = context.RecipeProvider.GetValue<float>(SuppPressMinSetValueRecipe);
        var downTol = context.RecipeProvider.GetValue<float>(SuppPressMinDownTolRecipe);
        var upTol = context.RecipeProvider.GetValue<float>(SuppPressMinUpTolRecipe);

        var min = setValue.HasValue ? setValue.Value - (downTol ?? 0) : 0f;
        var max = setValue.HasValue ? setValue.Value + (upTol ?? 0) : 0f;

        testResultsService.Add(
            parameterName: "Blr_Gas_Pres_Min",
            value: $"{value:F3}",
            min: $"{min:F3}",
            max: $"{max:F3}",
            status: status,
            isRanged: true,
            unit: "мбар");
    }

    /// <summary>
    /// Сохраняет результат Gas_Flow_Min (с пределами FlowMin).
    /// </summary>
    private void SaveGasFlowMinResult(TestStepContext context, float value, int status)
    {
        var setValue = context.RecipeProvider.GetValue<float>(FlowMinSetValueRecipe);
        var downTol = context.RecipeProvider.GetValue<float>(FlowMinDownTolRecipe);
        var upTol = context.RecipeProvider.GetValue<float>(FlowMinUpTolRecipe);

        var min = setValue.HasValue ? setValue.Value - (downTol ?? 0) : 0f;
        var max = setValue.HasValue ? setValue.Value + (upTol ?? 0) : 0f;

        testResultsService.Add(
            parameterName: "Gas_Flow_Min",
            value: $"{value:F3}",
            min: $"{min:F3}",
            max: $"{max:F3}",
            status: status,
            isRanged: true,
            unit: "л/мин");
    }

    /// <summary>
    /// Сохраняет результат Bnr_Gas_Pres_Min (с пределами BurnerPressMin).
    /// </summary>
    private void SaveBnrGasPressMinResult(TestStepContext context, float value, int status)
    {
        var setValue = context.RecipeProvider.GetValue<float>(BurnerPressMinSetValueRecipe);
        var downTol = context.RecipeProvider.GetValue<float>(BurnerPressMinDownTolRecipe);
        var upTol = context.RecipeProvider.GetValue<float>(BurnerPressMinUpTolRecipe);

        var min = setValue.HasValue ? setValue.Value - (downTol ?? 0) : 0f;
        var max = setValue.HasValue ? setValue.Value + (upTol ?? 0) : 0f;

        testResultsService.Add(
            parameterName: "Bnr_Gas_Pres_Min",
            value: $"{value:F3}",
            min: $"{min:F3}",
            max: $"{max:F3}",
            status: status,
            isRanged: true,
            unit: "мбар");
    }

    /// <summary>
    /// Скрывает оба слайдера.
    /// </summary>
    private async Task HideSlidersAsync(TestStepContext context)
    {
        try
        {
            await context.RangeSliderUiState.HideAsync(PressureSliderIndex, CancellationToken.None);
            await context.RangeSliderUiState.HideAsync(FlowSliderIndex, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Ошибка скрытия слайдеров: {Error}", ex.Message);
        }
    }

    private enum CompletionResult { Success, Error, Ready1 }
}

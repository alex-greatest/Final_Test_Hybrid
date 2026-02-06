using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Completion;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Limits;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.Gas;

/// <summary>
/// Тестовый шаг проверки давления и расхода газа на максимальной мощности.
/// Отображает два слайдера для визуализации параметров газа без сохранения результатов.
/// </summary>
public class CheckGasAndPBurnerMaxLevelsStep(
    DualLogger<CheckGasAndPBurnerMaxLevelsStep> logger)
    : ITestStep, IHasPlcBlockPath, IRequiresPlcSubscriptions, IRequiresRecipes, IProvideLimits
{
    private const string BlockPath = "DB_VI.Gas.Check_Gas_and_P_Burner_Max_Levels";
    private const string StartTag = "ns=3;s=\"DB_VI\".\"Gas\".\"Check_Gas_and_P_Burner_Max_Levels\".\"Start\"";
    private const string EndTag = "ns=3;s=\"DB_VI\".\"Gas\".\"Check_Gas_and_P_Burner_Max_Levels\".\"End\"";
    private const string ErrorTag = "ns=3;s=\"DB_VI\".\"Gas\".\"Check_Gas_and_P_Burner_Max_Levels\".\"Error\"";

    // Теги для слайдеров (привязка стрелки)
    private const string GasPgbTag = "ns=3;s=\"DB_Measure\".\"Sensor\".\"Gas_PGB\"";
    private const string GasPogTag = "ns=3;s=\"DB_Measure\".\"Sensor\".\"Gas_POG\"";

    // Теги для результатов (чтение из PLC)
    private const string BlrGasPressMaxTag = "ns=3;s=\"DB_Parameter\".\"Blr\".\"Gas_Pres_Max\"";
    private const string GasFlowMaxTag = "ns=3;s=\"DB_Parameter\".\"Gas\".\"Flow_Max\"";
    private const string BnrGasPressMaxTag = "ns=3;s=\"DB_Parameter\".\"Bnr\".\"Gas_Pres_Max\"";

    // Рецепты для слайдера 1 - Давление горелки (BurnerPressMax)
    private const string BurnerPressMaxSetValueRecipe = "ns=3;s=\"DB_Recipe\".\"Gas\".\"BurnerPressMax\".\"SetValue\"";
    private const string BurnerPressMaxDownTolRecipe = "ns=3;s=\"DB_Recipe\".\"Gas\".\"BurnerPressMax\".\"DownTol\"";
    private const string BurnerPressMaxUpTolRecipe = "ns=3;s=\"DB_Recipe\".\"Gas\".\"BurnerPressMax\".\"UpTol\"";

    // Рецепты для слайдера 2 - Расход газа (FlowMax)
    private const string FlowMaxSetValueRecipe = "ns=3;s=\"DB_Recipe\".\"Gas\".\"FlowMax\".\"SetValue\"";
    private const string FlowMaxDownTolRecipe = "ns=3;s=\"DB_Recipe\".\"Gas\".\"FlowMax\".\"DownTol\"";
    private const string FlowMaxUpTolRecipe = "ns=3;s=\"DB_Recipe\".\"Gas\".\"FlowMax\".\"UpTol\"";

    // Индексы слайдеров (используем 0 и 1 для одновременного показа двух слайдеров)
    private const int PressureSliderIndex = 0;
    private const int FlowSliderIndex = 1;

    public string Id => "gas-check-gas-and-p-burner-max-levels";
    public string Name => "Gas/Check_Gas_and_P_Burner_Max_Levels";
    public string Description => "Проверка давления и расхода газа на максимальной мощности.";
    public string PlcBlockPath => BlockPath;

    public IReadOnlyList<string> RequiredPlcTags =>
    [
        StartTag, EndTag, ErrorTag,
        GasPgbTag, GasPogTag,
        BlrGasPressMaxTag, GasFlowMaxTag, BnrGasPressMaxTag
    ];

    public IReadOnlyList<string> RequiredRecipeAddresses =>
    [
        BurnerPressMaxSetValueRecipe, BurnerPressMaxDownTolRecipe, BurnerPressMaxUpTolRecipe,
        FlowMaxSetValueRecipe, FlowMaxDownTolRecipe, FlowMaxUpTolRecipe
    ];

    /// <summary>
    /// Возвращает пределы для отображения в гриде.
    /// Показывает два параметра: Pгор (BurnerPressMax), Q (FlowMax).
    /// </summary>
    /// <param name="context">Контекст с доступом к рецептам.</param>
    /// <returns>Строка с пределами или null, если рецепты не загружены.</returns>
    public string? GetLimits(LimitsContext context)
    {
        // BurnerPressMax — для Bnr_Gas_Pres_Max (давление горелки)
        var burnerSetValue = context.RecipeProvider.GetValue<float>(BurnerPressMaxSetValueRecipe);
        var burnerDownTol = context.RecipeProvider.GetValue<float>(BurnerPressMaxDownTolRecipe);
        var burnerUpTol = context.RecipeProvider.GetValue<float>(BurnerPressMaxUpTolRecipe);

        // FlowMax — для Gas_Flow_Max (расход газа)
        var flowSetValue = context.RecipeProvider.GetValue<float>(FlowMaxSetValueRecipe);
        var flowDownTol = context.RecipeProvider.GetValue<float>(FlowMaxDownTolRecipe);
        var flowUpTol = context.RecipeProvider.GetValue<float>(FlowMaxUpTolRecipe);

        if (burnerSetValue == null || flowSetValue == null)
        {
            return null;
        }

        var burnerMin = burnerSetValue.Value - (burnerDownTol ?? 0);
        var burnerMax = burnerSetValue.Value + (burnerUpTol ?? 0);
        var flowMin = flowSetValue.Value - (flowDownTol ?? 0);
        var flowMax = flowSetValue.Value + (flowUpTol ?? 0);

        return $"PGB:[{burnerMin:F1}..{burnerMax:F1}]; POG:[{flowMin:F1}..{flowMax:F1}]";
    }

    /// <summary>
    /// Выполняет шаг проверки параметров газа на максимальной мощности.
    /// </summary>
    /// <param name="context">Контекст выполнения шага с доступом к OPC-UA и UI.</param>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат выполнения шага (Pass/Fail).</returns>
    /// <exception cref="OperationCanceledException">При отмене операции через ct.</exception>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Запуск проверки параметров газа в максимальном режиме");

        var pressureConfig = CreatePressureSliderConfig(context);
        var flowConfig = CreateFlowSliderConfig(context);

        if (pressureConfig == null || flowConfig == null)
        {
            return TestStepResult.Fail("Не удалось загрузить рецепты для слайдеров");
        }

        try
        {
            // Показываем слайдеры внутри try для гарантированного cleanup
            context.RangeSliderUiState.SetTitle("Настройка параметров газа в максимальном режиме");
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
        var setValue = context.RecipeProvider.GetValue<float>(BurnerPressMaxSetValueRecipe);
        var downTol = context.RecipeProvider.GetValue<float>(BurnerPressMaxDownTolRecipe);
        var upTol = context.RecipeProvider.GetValue<float>(BurnerPressMaxUpTolRecipe);

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
        var setValue = context.RecipeProvider.GetValue<float>(FlowMaxSetValueRecipe);
        var downTol = context.RecipeProvider.GetValue<float>(FlowMaxDownTolRecipe);
        var upTol = context.RecipeProvider.GetValue<float>(FlowMaxUpTolRecipe);

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
    /// Ожидает завершения операции проверки параметров газа.
    /// </summary>
    private async Task<TestStepResult> WaitForCompletionAsync(TestStepContext context, CancellationToken ct)
    {
        var waitResult = await context.TagWaiter.WaitAnyAsync(
            context.TagWaiter.CreateWaitGroup<CompletionResult>()
                .WaitForTrue(EndTag, () => CompletionResult.Success, "End")
                .WaitForTrue(ErrorTag, () => CompletionResult.Error, "Error"),
            ct);

        return waitResult.Result switch
        {
            CompletionResult.Success => await HandleCompletionAsync(context, isSuccess: true, ct),
            CompletionResult.Error => await HandleCompletionAsync(context, isSuccess: false, ct),
            _ => TestStepResult.Fail("Неизвестный результат")
        };
    }

    /// <summary>
    /// Обрабатывает завершение: чтение значений из PLC для логирования.
    /// </summary>
    private async Task<TestStepResult> HandleCompletionAsync(
        TestStepContext context,
        bool isSuccess,
        CancellationToken ct)
    {
        // Читаем результаты из PLC для логирования
        var (_, blrGasPress, blrError) = await context.OpcUa.ReadAsync<float>(BlrGasPressMaxTag, ct);
        if (blrError != null)
        {
            return TestStepResult.Fail($"Ошибка чтения Blr.Gas_Pres_Max: {blrError}");
        }

        var (_, gasFlow, flowError) = await context.OpcUa.ReadAsync<float>(GasFlowMaxTag, ct);
        if (flowError != null)
        {
            return TestStepResult.Fail($"Ошибка чтения Gas.Flow_Max: {flowError}");
        }

        var (_, bnrGasPress, bnrError) = await context.OpcUa.ReadAsync<float>(BnrGasPressMaxTag, ct);
        if (bnrError != null)
        {
            return TestStepResult.Fail($"Ошибка чтения Bnr.Gas_Pres_Max: {bnrError}");
        }

        logger.LogInformation(
            "Результаты проверки: Blr_Gas_Pres_Max={BlrPress:F3}, Gas_Flow_Max={Flow:F3}, Bnr_Gas_Pres_Max={BnrPress:F3}, статус={Status}",
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

    private enum CompletionResult { Success, Error }
}

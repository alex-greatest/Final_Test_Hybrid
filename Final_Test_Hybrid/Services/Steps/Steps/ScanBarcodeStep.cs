using Final_Test_Hybrid.Models;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Main.Messages;
using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.Preparation;
using Final_Test_Hybrid.Services.Results;
using Final_Test_Hybrid.Services.Scanner;
using Final_Test_Hybrid.Services.SpringBoot.Operator;
using Final_Test_Hybrid.Services.SpringBoot.Shift;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Validation;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Steps;

/// <summary>
/// Шаг сканирования для Non-MES режима.
/// Выполняет полную подготовку к тесту: валидация, поиск типа, загрузка рецептов, построение maps, инициализация БД.
/// </summary>
public class ScanBarcodeStep(
    BarcodeScanService barcodeScanService,
    ITestSequenceLoader sequenceLoader,
    ITestMapBuilder mapBuilder,
    ITestMapResolver mapResolver,
    RecipeValidator recipeValidator,
    BoilerState boilerState,
    PausableOpcUaTagService opcUa,
    IRecipeProvider recipeProvider,
    ITestResultsService testResultsService,
    ExecutionPhaseState phaseState,
    IScanPreparationFacade preparationFacade,
    OperatorState operatorState,
    ShiftState shiftState,
    ILogger<ScanBarcodeStep> logger,
    ITestStepLogger testStepLogger)
    : ScanStepBase(barcodeScanService, sequenceLoader, mapBuilder, mapResolver,
        recipeValidator, boilerState, opcUa, recipeProvider, testResultsService, phaseState)
{
    private readonly DualLogger<ScanBarcodeStep> _logger = new(logger, testStepLogger);

    private const string UnknownOperator = "Unknown";

    public override string Id => "scan-barcode";
    public override string Name => "Сканирование штрихкода";
    public override string Description => "Сканирует штрихкод и выполняет подготовку к тесту";
    protected override IDualLogger Logger => _logger;

    public override async Task<PreExecutionResult> ExecuteAsync(PreExecutionContext context, CancellationToken ct)
    {
        _logger.LogStepStart(Name);
        PhaseState.SetPhase(ExecutionPhase.BarcodeReceived);

        // 1. Валидация баркода
        var validateError = ValidateBarcode(context);
        if (validateError != null)
        {
            return validateError;
        }

        // 2-3. Загрузка данных котла (facade)
        var loadError = await preparationFacade.LoadBoilerDataAsync(context);
        if (loadError != null)
        {
            return loadError;
        }

        ct.ThrowIfCancellationRequested();

        // 4. Загрузка последовательности тестов
        var loadSequenceError = await LoadTestSequenceAsync(context);
        if (loadSequenceError != null)
        {
            return loadSequenceError;
        }

        ct.ThrowIfCancellationRequested();

        // 5. Построение карт тестов
        var buildMapsError = BuildTestMaps(context);
        if (buildMapsError != null)
        {
            return buildMapsError;
        }

        // 6. Сохранение состояния котла
        SaveBoilerState(context);

        // 7. Резолв карт тестов
        var resolveMapsError = ResolveTestMaps(context);
        if (resolveMapsError != null)
        {
            return resolveMapsError;
        }

        // 8. Валидация рецептов
        var validateRecipesError = ValidateRecipes(context);
        if (validateRecipesError != null)
        {
            return validateRecipesError;
        }

        // 9. Инициализация БД (facade)
        PhaseState.SetPhase(ExecutionPhase.CreatingDbRecords);
        var initDbError = await preparationFacade.InitializeDatabaseAsync(
            BoilerState,
            operatorState.Username ?? UnknownOperator,
            shiftState.ShiftNumber ?? 0);
        if (initDbError != null)
        {
            return initDbError;
        }

        ct.ThrowIfCancellationRequested();

        // 10. Запись рецептов в PLC
        var writePlcError = await WriteRecipesToPlcAsync(context, ct);
        if (writePlcError != null)
        {
            return writePlcError;
        }

        // 11. Инициализация провайдера рецептов
        InitializeRecipeProvider();
        SaveScanMetadata(operatorState.Username ?? UnknownOperator, shiftState.ShiftNumber);

        var pressureError = await ReadAndSavePressuresAsync(ct);
        if (pressureError != null)
        {
            return pressureError;
        }

        _logger.LogStepEnd(Name);
        return PreExecutionResult.Continue(context.Barcode);
    }
}

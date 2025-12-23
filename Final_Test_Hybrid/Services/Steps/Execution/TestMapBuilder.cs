using Final_Test_Hybrid.Services.Steps.Interaces;
using Final_Test_Hybrid.Services.Steps.Models;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Execution;

public class TestMapBuilder(
    ITestStepRegistry registry,
    SequenceValidationState validationState,
    ILogger<TestMapBuilder> logger) : ITestMapBuilder
{
    private const int ColumnCount = 4;
    private const string PlaceholderName = "<TEST STEP>";

    public List<TestMap>? Build(List<string?[]> rawData)
    {
        logger.LogInformation("Создание Maps из {Count} строк", rawData.Count);
        var result = BuildMapsFromRawData(rawData);
        if (result == null)
        {
            return null;
        }
        logger.LogInformation("Создано {Count} Maps", result.Count);
        return result;
    }

    private List<TestMap>? BuildMapsFromRawData(List<string?[]> rawData)
    {
        var context = new MapBuildContext();
        var allRowsProcessed = ProcessAllRows(rawData, context);
        if (!allRowsProcessed)
        {
            return null;
        }
        FinalizeCurrentMap(context);
        return context.Maps;
    }

    private bool ProcessAllRows(List<string?[]> rawData, MapBuildContext context)
    {
        return rawData
            .Select((cells, index) => ProcessRow(cells, index + 1, context))
            .All(success => success);
    }

    private bool ProcessRow(string?[] cells, int rowNumber, MapBuildContext context)
    {
        if (IsPlaceholderRow(cells))
        {
            FinalizeCurrentMap(context);
            return true;
        }
        return AddRowToCurrentMap(cells, rowNumber, context);
    }

    private bool AddRowToCurrentMap(string?[] cells, int rowNumber, MapBuildContext context)
    {
        var mapRow = BuildMapRow(cells, rowNumber);
        if (mapRow == null)
        {
            return false;
        }
        context.CurrentRows.Add(mapRow);
        return true;
    }

    private void FinalizeCurrentMap(MapBuildContext context)
    {
        if (context.CurrentRows.Count == 0)
        {
            return;
        }
        var map = CreateMap(context.CurrentRows, context.NextMapIndex);
        context.Maps.Add(map);
        context.NextMapIndex++;
        context.CurrentRows = [];
    }

    private static TestMap CreateMap(List<TestMapRow> rows, int index)
    {
        return new TestMap
        {
            Index = index,
            Rows = rows
        };
    }

    private static bool IsPlaceholderRow(string?[] cells)
    {
        return cells.All(c => string.Equals(c?.Trim(), PlaceholderName, StringComparison.OrdinalIgnoreCase));
    }

    private TestMapRow? BuildMapRow(string?[] cells, int rowNumber)
    {
        var steps = ResolveAllSteps(cells, rowNumber);
        return steps == null ? null : CreateMapRow(steps, rowNumber);
    }

    private ITestStep?[]? ResolveAllSteps(string?[] cells, int rowNumber)
    {
        var steps = new ITestStep?[ColumnCount];
        var resolutionResults = cells
            .Select((cell, colIndex) => ResolveStepWithValidation(cell, rowNumber, colIndex + 1))
            .ToList();
        return resolutionResults.Any(r => r.HasError) ? null : resolutionResults.Select(r => r.Step).ToArray();
    }

    private StepResolutionResult ResolveStepWithValidation(string? stepName, int rowNumber, int colNumber)
    {
        if (string.IsNullOrWhiteSpace(stepName))
        {
            return StepResolutionResult.Empty();
        }
        var step = registry.GetByName(stepName.Trim());
        if (step != null)
        {
            return StepResolutionResult.Success(step);
        }
        ReportUnknownStepError(stepName, rowNumber, colNumber);
        return StepResolutionResult.Error();
    }

    private void ReportUnknownStepError(string stepName, int rowNumber, int colNumber)
    {
        var error = $"Неизвестный шаг '{stepName}' в строке {rowNumber}, колонке {colNumber}";
        logger.LogWarning("Неизвестный шаг '{StepName}' в строке {Row}, колонке {Col}", stepName, rowNumber, colNumber);
        validationState.SetError(error);
    }

    private static TestMapRow CreateMapRow(ITestStep?[] steps, int rowNumber)
    {
        var mapRow = new TestMapRow { RowIndex = rowNumber };
        CopyStepsToMapRow(steps, mapRow);
        return mapRow;
    }

    private static void CopyStepsToMapRow(ITestStep?[] steps, TestMapRow mapRow)
    {
        for (var col = 0; col < ColumnCount; col++)
        {
            mapRow.Steps[col] = steps[col];
        }
    }

    private class MapBuildContext
    {
        public List<TestMap> Maps { get; } = [];
        public List<TestMapRow> CurrentRows { get; set; } = [];
        public int NextMapIndex { get; set; }
    }

    private readonly struct StepResolutionResult
    {
        public ITestStep? Step { get; }
        public bool HasError { get; }

        private StepResolutionResult(ITestStep? step, bool hasError)
        {
            Step = step;
            HasError = hasError;
        }

        public static StepResolutionResult Empty() => new(null, false);
        public static StepResolutionResult Success(ITestStep step) => new(step, false);
        public static StepResolutionResult Error() => new(null, true);
    }
}

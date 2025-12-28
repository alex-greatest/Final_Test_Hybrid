using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Base;

public class TestMapBuilder(
    ITestStepLogger testLogger,
    ILogger<TestMapBuilder> logger) : ITestMapBuilder
{
    private const int ColumnCount = 4;
    private const string PlaceholderName = "<TEST STEP>";

    public RawMapBuildResult Build(List<string?[]> rawData)
    {
        LogBuildStart(rawData.Count);
        var context = new BuildContext();
        var validationError = ProcessAllRows(rawData, context);
        if (validationError != null)
        {
            return RawMapBuildResult.WithError(validationError);
        }
        FinalizeCurrentMap(context);
        LogBuildComplete(context.Maps.Count);
        return RawMapBuildResult.Success(context.Maps);
    }

    private void LogBuildStart(int rowCount)
    {
        logger.LogInformation("Парсинг {Count} строк", rowCount);
        testLogger.LogInformation("Обработка файла тестов ({Count} строк)...", rowCount);
    }

    private void LogBuildComplete(int mapCount)
    {
        logger.LogInformation("Создано {Count} RawMaps", mapCount);
        testLogger.LogInformation("Структура тестов разобрана: {Count} блоков", mapCount);
    }

    private string? ProcessAllRows(List<string?[]> rawData, BuildContext context)
    {
        for (var index = 0; index < rawData.Count; index++)
        {
            var rowNumber = index + 1;
            var error = ProcessSingleRow(rawData[index], rowNumber, context);
            if (error != null)
            {
                return error;
            }
        }
        return null;
    }

    private string? ProcessSingleRow(string?[] cells, int rowNumber, BuildContext context)
    {
        var validationError = ValidatePlaceholderStructure(cells, rowNumber);
        if (validationError != null)
        {
            return validationError;
        }
        if (IsMapSeparator(cells))
        {
            FinalizeCurrentMap(context);
            return null;
        }
        AppendRowToCurrentMap(cells, rowNumber, context);
        return null;
    }

    private string? ValidatePlaceholderStructure(string?[] cells, int rowNumber)
    {
        var placeholderCount = CountPlaceholders(cells);
        return IsValidPlaceholderCount(placeholderCount) ? null : CreatePlaceholderError(rowNumber);
    }

    private static int CountPlaceholders(string?[] cells) =>
        cells.Count(IsPlaceholder);

    private static bool IsValidPlaceholderCount(int count) =>
        count is 0 or ColumnCount;

    private string CreatePlaceholderError(int rowNumber)
    {
        logger.LogError("Нарушена структура <TEST STEP> в строке {Row}", rowNumber);
        testLogger.LogError(null, "Ошибка структуры файла: строка {Row} - некорректный разделитель блоков", rowNumber);
        return $"Строка {rowNumber}: <TEST STEP> должен быть во всех 4 колонках";
    }

    private static void AppendRowToCurrentMap(string?[] cells, int rowNumber, BuildContext context)
    {
        var row = new RawTestMapRow(rowNumber, cells);
        context.CurrentRows.Add(row);
    }

    private static void FinalizeCurrentMap(BuildContext context)
    {
        if (context.CurrentRows.Count == 0)
        {
            return;
        }
        var map = new RawTestMap(context.NextMapIndex, context.CurrentRows);
        context.Maps.Add(map);
        context.NextMapIndex++;
        context.CurrentRows = [];
    }

    private static bool IsMapSeparator(string?[] cells) =>
        cells.All(IsPlaceholder);

    private static bool IsPlaceholder(string? value) =>
        string.Equals(value?.Trim(), PlaceholderName, StringComparison.OrdinalIgnoreCase);

    private class BuildContext
    {
        public List<RawTestMap> Maps { get; } = [];
        public List<RawTestMapRow> CurrentRows { get; set; } = [];
        public int NextMapIndex { get; set; }
    }
}

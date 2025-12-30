using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.Test;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Base;

public class TestMapResolver(
    ITestStepRegistry registry,
    ITestStepLogger testLogger,
    ILogger<TestMapResolver> logger) : ITestMapResolver
{
    private const int ColumnCount = 4;

    public ResolveResult Resolve(List<RawTestMap> rawMaps)
    {
        LogResolvingStart(rawMaps.Count);
        var context = new ResolveContext();
        var maps = rawMaps.Select(raw => ResolveMap(raw, context)).ToList();
        LogResolvingComplete(maps.Count, context.UnknownSteps.Count);
        return BuildResult(maps, context.UnknownSteps);
    }

    private void LogResolvingStart(int count)
    {
        logger.LogInformation("Резолв {Count} Maps", count);
        testLogger.LogInformation("Поиск шагов тестирования ({Count} блоков)...", count);
    }

    private void LogResolvingComplete(int mapsCount, int unknownCount)
    {
        logger.LogInformation(
            "Резолв завершён: {Maps} maps, {Unknown} неизвестных шагов",
            mapsCount, unknownCount);
        if (unknownCount > 0)
        {
            testLogger.LogWarning("Найдено {Count} неизвестных шагов", unknownCount);
        }
        else
        {
            testLogger.LogInformation("Все шаги успешно найдены");
        }
    }

    private static ResolveResult BuildResult(List<TestMap> maps, List<UnknownStepInfo> unknownSteps) =>
        unknownSteps.Count > 0
            ? ResolveResult.WithUnknownSteps(maps, unknownSteps)
            : ResolveResult.Success(maps);

    private TestMap ResolveMap(RawTestMap raw, ResolveContext context) =>
        new()
        {
            Index = raw.Index,
            Rows = raw.Rows.Select(row => ResolveRow(row, context)).ToList()
        };

    private TestMapRow ResolveRow(RawTestMapRow raw, ResolveContext context)
    {
        var mapRow = new TestMapRow { RowIndex = raw.RowIndex };
        for (var col = 0; col < ColumnCount; col++)
        {
            TryResolveCell(raw.StepNames[col], raw.RowIndex, col, mapRow, context);
        }
        return mapRow;
    }

    private void TryResolveCell(
        string? stepName,
        int rowIndex,
        int columnIndex,
        TestMapRow mapRow,
        ResolveContext context)
    {
        if (string.IsNullOrWhiteSpace(stepName))
        {
            return;
        }
        var normalizedName = stepName.Trim();
        var step = registry.GetByName(normalizedName);
        if (step != null)
        {
            mapRow.Steps[columnIndex] = step;
            return;
        }
        RegisterUnknownStep(normalizedName, rowIndex, columnIndex, context);
    }

    private void RegisterUnknownStep(
        string stepName,
        int rowIndex,
        int columnIndex,
        ResolveContext context)
    {
        var displayColumn = columnIndex + 1;
        logger.LogWarning(
            "Неизвестный шаг '{StepName}' в строке {Row}, колонке {Col}",
            stepName, rowIndex, displayColumn);
        testLogger.LogWarning(
            "Неизвестный шаг '{StepName}' (строка {Row}, колонка {Col})",
            stepName, rowIndex, displayColumn);
        context.UnknownSteps.Add(new UnknownStepInfo(stepName, rowIndex, displayColumn));
    }

    private class ResolveContext
    {
        public List<UnknownStepInfo> UnknownSteps { get; } = [];
    }
}

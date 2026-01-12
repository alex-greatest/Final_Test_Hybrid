using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;

namespace Final_Test_Hybrid.Services.Steps.Steps.PreExecution;

public class LoadTestSequenceStep(
    ITestSequenceLoader sequenceLoader,
    DualLogger<LoadTestSequenceStep> logger) : IPreExecutionStep
{
    public string Id => "load-test-sequence";
    public string Name => "Загрузка последовательности";
    public string Description => "Загрузка последовательности тестов из Excel";
    public bool IsVisibleInStatusGrid => false;

    public async Task<PreExecutionResult> ExecuteAsync(PreExecutionContext context, CancellationToken ct)
    {
        var article = GetArticle(context);
        var result = await sequenceLoader.LoadRawDataAsync(article);
        if (!result.IsSuccess)
        {
            return PreExecutionResult.Fail(result.Error!);
        }
        context.RawSequenceData = result.RawData;
        logger.LogInformation("Последовательность загружена: {Rows} строк", result.RawData!.Count);
        return PreExecutionResult.Continue();
    }

    private static string GetArticle(PreExecutionContext context)
    {
        // MES режим: артикул из BoilerTypeCycle
        // Non-MES режим: артикул из BarcodeValidation
        return context.BoilerTypeCycle?.Article ?? context.BarcodeValidation!.Article!;
    }
}

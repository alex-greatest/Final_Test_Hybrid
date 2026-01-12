using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Database;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;

namespace Final_Test_Hybrid.Services.Steps.Steps.PreExecution;

public class FindBoilerTypeStep(
    BoilerTypeService boilerTypeService,
    DualLogger<FindBoilerTypeStep> logger) : IPreExecutionStep
{
    public string Id => "find-boiler-type";
    public string Name => "Поиск типа котла";
    public string Description => "Поиск типа котла в базе данных";
    public bool IsVisibleInStatusGrid => false;

    public async Task<PreExecutionResult> ExecuteAsync(PreExecutionContext context, CancellationToken ct)
    {
        var article = context.BarcodeValidation!.Article!;
        var cycle = await boilerTypeService.FindActiveByArticleAsync(article);
        if (cycle == null)
        {
            return PreExecutionResult.Fail($"Тип котла не найден для артикула: {article}");
        }
        context.BoilerTypeCycle = cycle;
        logger.LogInformation("Тип котла найден: {Type}, артикул: {Article}", cycle.Type, cycle.Article);
        return PreExecutionResult.Continue();
    }
}

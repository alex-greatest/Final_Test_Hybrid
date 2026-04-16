using Final_Test_Hybrid.Models;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Results;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.Coms;

/// <summary>
/// Чтение артикула без диагностической связи с котлом.
/// </summary>
public class ReadArticleWithoutConnectionStep(
    BoilerState boilerState,
    ITestResultsService testResultsService,
    DualLogger<ReadArticleWithoutConnectionStep> logger) : ITestStep
{
    private const string ResultName = "Article_Without_Connection";

    public string Id => "coms-read-article-without-connection";
    public string Name => "Coms/Read_Article_Without_Connection";
    public string Description => "Чтение артикула без связи с котлом";

    /// <summary>
    /// Сохраняет артикул текущего котла из runtime-состояния.
    /// </summary>
    public Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var article = boilerState.Article;
        if (string.IsNullOrWhiteSpace(article))
        {
            const string message = "Артикул не задан в BoilerState";
            logger.LogError(message);
            return Task.FromResult(TestStepResult.Fail(message, canSkip: false));
        }

        testResultsService.Remove(ResultName);
        testResultsService.Add(
            parameterName: ResultName,
            value: article,
            min: "",
            max: "",
            status: 1,
            isRanged: false,
            unit: "",
            test: Name);

        logger.LogInformation("Артикул без связи с котлом: {Article}", article);
        return Task.FromResult(TestStepResult.Pass(article));
    }
}

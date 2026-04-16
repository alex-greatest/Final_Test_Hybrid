using Final_Test_Hybrid.Models;
using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Services.Results;
using Final_Test_Hybrid.Services.SpringBoot.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;
using Final_Test_Hybrid.Services.Steps.Steps.Coms;
using Final_Test_Hybrid.Settings.App;
using Final_Test_Hybrid.Tests.TestSupport;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class ReadArticleWithoutConnectionStepTests
{
    private const string Article = "1234567890";
    private const string ResultName = "Article_Without_Connection";
    private const string StepName = "Coms/Read_Article_Without_Connection";

    [Fact]
    public async Task ExecuteAsync_SavesBoilerArticle()
    {
        var boilerState = CreateBoilerState();
        boilerState.SetData("barcode", Article, true);
        var results = new TestResultsService();
        var step = CreateStep(boilerState, results);

        var result = await step.ExecuteAsync(null!, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(Article, result.Message);
        AssertResult(results, Article);
    }

    [Fact]
    public async Task ExecuteAsync_ClearsPreviousResultBeforeSaving()
    {
        var boilerState = CreateBoilerState();
        boilerState.SetData("barcode", Article, true);
        var results = new TestResultsService();
        results.Add(ResultName, "old", "", "", 1, false, "", StepName);
        var step = CreateStep(boilerState, results);

        var result = await step.ExecuteAsync(null!, CancellationToken.None);

        Assert.True(result.Success);
        AssertResult(results, Article);
    }

    [Fact]
    public async Task ExecuteAsync_FailsWithoutSaving_WhenArticleIsEmpty()
    {
        var boilerState = CreateBoilerState();
        var results = new TestResultsService();
        var step = CreateStep(boilerState, results);

        var result = await step.ExecuteAsync(null!, CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(result.CanSkip);
        Assert.Equal("Артикул не задан в BoilerState", result.Message);
        Assert.Empty(results.GetResults());
    }

    private static ReadArticleWithoutConnectionStep CreateStep(
        BoilerState boilerState,
        ITestResultsService results)
    {
        return new ReadArticleWithoutConnectionStep(
            boilerState,
            results,
            TestInfrastructure.CreateDualLogger<ReadArticleWithoutConnectionStep>());
    }

    private static BoilerState CreateBoilerState()
    {
        return new BoilerState(
            new AppSettingsService(Options.Create(new AppSettings())),
            new EmptyRecipeProvider());
    }

    private static void AssertResult(TestResultsService results, string expectedValue)
    {
        var item = Assert.Single(results.GetResults(), x => x.ParameterName == ResultName);
        Assert.Equal(expectedValue, item.Value);
        Assert.Equal(StepName, item.Test);
        Assert.Equal(1, item.Status);
        Assert.False(item.IsRanged);
        Assert.Equal("", item.Min);
        Assert.Equal("", item.Max);
        Assert.Equal("", item.Unit);
    }

    private sealed class EmptyRecipeProvider : IRecipeProvider
    {
        public RecipeResponseDto? GetByAddress(string address) => null;

        public IReadOnlyList<RecipeResponseDto> GetAll() => [];

        public void SetRecipes(IReadOnlyList<RecipeResponseDto> recipes)
        {
        }

        public void Clear()
        {
        }

        public T? GetValue<T>(string address) where T : struct => null;

        public string? GetStringValue(string address) => null;
    }
}

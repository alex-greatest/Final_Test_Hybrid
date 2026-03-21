using Final_Test_Hybrid.Models;
using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Services.SpringBoot.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;
using Final_Test_Hybrid.Settings.App;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class BoilerStateTests
{
    [Fact]
    public async Task StopTestTimer_FreezesDurationUntilClear()
    {
        var boilerState = CreateBoilerState();

        boilerState.StartTestTimer();
        await Task.Delay(1100);
        boilerState.StopTestTimer();

        var stoppedDuration = boilerState.GetTestDuration();

        await Task.Delay(1200);

        var durationAfterWait = boilerState.GetTestDuration();

        Assert.InRange(stoppedDuration.TotalMilliseconds, 900, 2500);
        Assert.InRange(
            Math.Abs((durationAfterWait - stoppedDuration).TotalMilliseconds),
            0,
            250);
    }

    private static BoilerState CreateBoilerState()
    {
        var appSettings = new AppSettingsService(Options.Create(new AppSettings()));
        return new BoilerState(appSettings, new TestRecipeProvider());
    }

    private sealed class TestRecipeProvider : IRecipeProvider
    {
        public RecipeResponseDto? GetByAddress(string address)
        {
            return null;
        }

        public IReadOnlyList<RecipeResponseDto> GetAll()
        {
            return [];
        }

        public void SetRecipes(IReadOnlyList<RecipeResponseDto> recipes)
        {
        }

        public void Clear()
        {
        }

        public T? GetValue<T>(string address) where T : struct
        {
            return null;
        }

        public string? GetStringValue(string address)
        {
            return null;
        }
    }
}

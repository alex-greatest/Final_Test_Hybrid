using Final_Test_Hybrid.Services.Results;
using Final_Test_Hybrid.Services.SpringBoot.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Limits;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Final_Test_Hybrid.Services.Steps.Steps.DHW;
using Final_Test_Hybrid.Tests.TestSupport;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class SetCircuitPressureStepTests
{
    [Fact]
    public void GetLimits_UsesTargetPlusMinusTolerance()
    {
        var step = CreateStep();
        var context = CreateLimitsContext(value: "2.5", tolerance: "0.5");

        var limits = step.GetLimits(context);

        Assert.Equal("[2,0 .. 3,0]", limits);
    }

    [Fact]
    public void GetLimits_UsesActualRecipeToleranceWithoutHardcode()
    {
        var step = CreateStep();
        var context = CreateLimitsContext(value: "2.5", tolerance: "0.3");

        var limits = step.GetLimits(context);

        Assert.Equal("[2,2 .. 2,8]", limits);
    }

    private static SetCircuitPressureStep CreateStep()
    {
        return new SetCircuitPressureStep(
            TestInfrastructure.CreateDualLogger<SetCircuitPressureStep>(),
            new TestResultsService());
    }

    private static LimitsContext CreateLimitsContext(string value, string tolerance)
    {
        var provider = new RecipeProvider(
            TestInfrastructure.CreateLogger<RecipeProvider>(),
            new TestStepLoggerStub());
        provider.SetRecipes(
        [
            new RecipeResponseDto
            {
                Address = "ns=3;s=\"DB_Recipe\".\"DHW\".\"PresTest\".\"Value\"",
                Value = value
            },
            new RecipeResponseDto
            {
                Address = "ns=3;s=\"DB_Recipe\".\"DHW\".\"PresTest\".\"Tol\"",
                Value = tolerance
            }
        ]);

        return new LimitsContext
        {
            ColumnIndex = 0,
            RecipeProvider = provider
        };
    }
}

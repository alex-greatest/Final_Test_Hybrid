using Final_Test_Hybrid.Services.Results;
using Final_Test_Hybrid.Services.SpringBoot.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Limits;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Final_Test_Hybrid.Services.Steps.Steps.DHW;
using Final_Test_Hybrid.Tests.TestSupport;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class TankModeStepLimitsTests
{
    [Fact]
    public void SetTankModeStep_GetLimits_UsesWaterMinAndWaterMax()
    {
        var step = new SetTankModeStep(
            TestInfrastructure.CreateDualLogger<SetTankModeStep>(),
            new TestResultsService());
        var context = CreateLimitsContext(
            ("ns=3;s=\"DB_Recipe\".\"DHW\".\"Tank\".\"WaterMin\"", "5.0"),
            ("ns=3;s=\"DB_Recipe\".\"DHW\".\"Tank\".\"WaterMax\"", "20.0"),
            ("ns=3;s=\"DB_Recipe\".\"DHW\".\"Tank\".\"Mode\"", "2.5"));

        var limits = step.GetLimits(context);

        Assert.Equal("[5,0 .. 20,0]", limits);
    }

    [Fact]
    public void SetTankModeStep_RequiresWaterMinAndWaterMaxRecipes()
    {
        var step = new SetTankModeStep(
            TestInfrastructure.CreateDualLogger<SetTankModeStep>(),
            new TestResultsService());

        Assert.Equal(2, step.RequiredRecipeAddresses.Count);
        Assert.Equal("ns=3;s=\"DB_Recipe\".\"DHW\".\"Tank\".\"WaterMin\"", step.RequiredRecipeAddresses[0]);
        Assert.Equal("ns=3;s=\"DB_Recipe\".\"DHW\".\"Tank\".\"WaterMax\"", step.RequiredRecipeAddresses[1]);
    }

    [Fact]
    public void CheckTankModeStep_GetLimits_UsesTankModeRecipePlusMinusTolerance()
    {
        var step = new CheckTankModeStep(
            TestInfrastructure.CreateDualLogger<CheckTankModeStep>(),
            new TestResultsService());
        var context = CreateLimitsContext(
            ("ns=3;s=\"DB_Recipe\".\"DHW\".\"Tank\".\"Mode\"", "2.4"),
            ("ns=3;s=\"DB_Recipe\".\"DHW\".\"PresTest\".\"Value\"", "9.9"),
            ("ns=3;s=\"DB_Recipe\".\"DHW\".\"PresTest\".\"Tol\"", "0.3"));

        var limits = step.GetLimits(context);

        Assert.Equal("[2,1 .. 2,7]", limits);
    }

    [Fact]
    public void CheckTankModeStep_GetLimits_UsesActualToleranceWithoutHardcode()
    {
        var step = new CheckTankModeStep(
            TestInfrastructure.CreateDualLogger<CheckTankModeStep>(),
            new TestResultsService());
        var context = CreateLimitsContext(
            ("ns=3;s=\"DB_Recipe\".\"DHW\".\"Tank\".\"Mode\"", "2.5"),
            ("ns=3;s=\"DB_Recipe\".\"DHW\".\"PresTest\".\"Tol\"", "0.5"));

        var limits = step.GetLimits(context);

        Assert.Equal("[2,0 .. 3,0]", limits);
    }

    private static LimitsContext CreateLimitsContext(params (string Address, string Value)[] recipes)
    {
        var provider = new RecipeProvider(
            TestInfrastructure.CreateLogger<RecipeProvider>(),
            new TestStepLoggerStub());
        provider.SetRecipes(
        [
            .. recipes.Select(recipe => new RecipeResponseDto
            {
                Address = recipe.Address,
                Value = recipe.Value
            })
        ]);

        return new LimitsContext
        {
            ColumnIndex = 0,
            RecipeProvider = provider
        };
    }
}

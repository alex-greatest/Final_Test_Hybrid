using System.Reflection;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Final_Test_Hybrid.Services.Steps.Steps.Coms;
using Final_Test_Hybrid.Tests.TestSupport;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class ChStartMaxHeatoutWithoutStepTests
{
    [Fact]
    public void Metadata_UsesSeparatePlcOnlyContract()
    {
        var step = CreateStep();

        Assert.Equal("coms-ch-start-max-heatout-without", step.Id);
        Assert.Equal("Coms/CH_Start_Max_Heatout_Without", step.Name);
        Assert.Equal("Запуск максимального нагрева контура отопления без связи с котлом", step.Description);
        Assert.Equal("DB_Coms.DB_CH_Start_Max_Heatout_Without", step.PlcBlockPath);
        Assert.Equal(
            [
                "ns=3;s=\"DB_Coms\".\"DB_CH_Start_Max_Heatout_Without\".\"Start\"",
                "ns=3;s=\"DB_Coms\".\"DB_CH_Start_Max_Heatout_Without\".\"End\"",
                "ns=3;s=\"DB_Coms\".\"DB_CH_Start_Max_Heatout_Without\".\"Error\""
            ],
            step.RequiredPlcTags);
    }

    [Fact]
    public void RegistryScanning_IncludesSeparateStepWithoutNameCollision()
    {
        var isStepType = InvokeRegistryTypeFilter(typeof(ChStartMaxHeatoutWithoutStep));
        var newStep = CreateStep();

        Assert.True(isStepType);
        Assert.NotEqual("coms-ch-start-max-heatout", newStep.Id);
        Assert.NotEqual("Coms/CH_Start_Max_Heatout", newStep.Name);
    }

    [Fact]
    public void Interfaces_DoNotMakeStepNonSkippable()
    {
        ITestStep step = CreateStep();

        Assert.IsAssignableFrom<ITestStep>(step);
        Assert.IsAssignableFrom<IHasPlcBlockPath>(step);
        Assert.IsAssignableFrom<IRequiresPlcSubscriptions>(step);
        Assert.False(step is INonSkippable);
    }

    private static ChStartMaxHeatoutWithoutStep CreateStep()
    {
        return new ChStartMaxHeatoutWithoutStep(
            TestInfrastructure.CreateDualLogger<ChStartMaxHeatoutWithoutStep>());
    }

    private static bool InvokeRegistryTypeFilter(Type type)
    {
        var method = typeof(TestStepRegistry).GetMethod(
            "IsTestStepType",
            BindingFlags.Static | BindingFlags.NonPublic);

        var result = method?.Invoke(null, [type]);
        return Assert.IsType<bool>(result);
    }
}

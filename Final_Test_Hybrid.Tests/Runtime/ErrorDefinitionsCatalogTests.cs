using System.Reflection;
using Final_Test_Hybrid.Models.Errors;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class ErrorDefinitionsCatalogTests
{
    [Fact]
    public void All_ContainsAllDeclaredDefinitions_ExceptIntentionalSpecialCases()
    {
        var declared = GetDeclaredDefinitions()
            .ToDictionary(pair => pair.Name, pair => pair.Definition);

        var allByCode = ErrorDefinitions.All
            .ToDictionary(error => error.Code);

        var excludedNames = new HashSet<string>
        {
            nameof(ErrorDefinitions.EcuE9Stb),
            nameof(ErrorDefinitions.AlNotStendReadySafetyTime),
            nameof(ErrorDefinitions.AlCloseTimeSafetyTime)
        };

        foreach (var pair in declared)
        {
            if (excludedNames.Contains(pair.Key))
            {
                continue;
            }

            Assert.Contains(pair.Value.Code, allByCode.Keys);
        }

        Assert.DoesNotContain(
            ErrorDefinitions.All,
            error => error.Code == ErrorDefinitions.EcuE9Stb.Code);
        Assert.DoesNotContain(
            ErrorDefinitions.All,
            error => error.Code == ErrorDefinitions.AlNotStendReadySafetyTime.Code);
        Assert.DoesNotContain(
            ErrorDefinitions.All,
            error => error.Code == ErrorDefinitions.AlCloseTimeSafetyTime.Code);
    }

    [Fact]
    public void DeferredPlcErrors_StayInAllAndRemainUniqueByCode()
    {
        var allCodes = ErrorDefinitions.All
            .Select(error => error.Code)
            .ToHashSet();

        foreach (var error in ErrorDefinitions.DeferredPlcErrors)
        {
            Assert.Contains(error.Code, allCodes);
        }

        var duplicateCodes = ErrorDefinitions.All
            .GroupBy(error => error.Code)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        Assert.Empty(duplicateCodes);
    }

    [Theory]
    [InlineData(
        "ns=3;s=\"DB_CH\".\"DB_CH_Slow_Fill_Circuit\".\"Al_WaterPressureHight\"",
        "П-301-03",
        "Высокое давление воды")]
    [InlineData(
        "ns=3;s=\"DB_CH\".\"DB_CH_Compare_Flow_NTC_Temp_Hot\".\"Al_LowTemp\"",
        "П-305-10",
        "Неисправность. Заданная температура воды не достигнута")]
    [InlineData(
        "ns=3;s=\"DB_CH\".\"DB_CH_Check_Flow_Temperature_Rise\".\"Al_LowTemp\"",
        "П-307-11",
        "Неисправность. Заданная температура не достигнута")]
    [InlineData(
        "ns=3;s=\"DB_DHW\".\"DB_DHW_Get_Flow_NTC_Cold\".\"Al_WaterFlowMin\"",
        "П-206-01",
        "Неисправность. Слишком малый расход воды")]
    [InlineData(
        "ns=3;s=\"DB_DHW\".\"DB_DHW_Get_Flow_NTC_Cold\".\"Al_WaterFlowMax\"",
        "П-206-02",
        "Неисправность. Слишком большой расход воды")]
    [InlineData(
        "ns=3;s=\"DB_DHW\".\"DB_DHW_Check_Flow_Temperature_Rise\".\"Al_LowTemp\"",
        "П-205-03",
        "Неисправность. Заданная температура не достигнута")]
    [InlineData(
        "ns=3;s=\"DB_DHW\".\"DB_DHW_Compare_Flow_NTC_Temp_Hot\".\"Al_LowTemp\"",
        "П-208-02",
        "Неисправность. Заданная температура воды не достигнута")]
    [InlineData(
        "ns=3;s=\"DB_DHW\".\"DB_DHW_Compare_Flow_NTC_Temp_Hot\".\"Al_WaterFlowMin\"",
        "П-208-03",
        "Неисправность. Слишком малый расход воды")]
    [InlineData(
        "ns=3;s=\"DB_DHW\".\"DB_DHW_Compare_Flow_NTC_Temp_Hot\".\"Al_WaterFlowMax\"",
        "П-208-04",
        "Неисправность. Слишком большой расход воды")]
    [InlineData(
        "ns=3;s=\"DB_Coms\".\"DB_CH_Start_Max_Heatout_Without\".\"Al_NoWaterFlow\"",
        "П-109-02",
        "Неисправность. Нет протока воды")]
    [InlineData(
        "ns=3;s=\"DB_DHW\".\"DB_Set_Tank_Mode\".\"Al_PressureHight\"",
        "П-213-02",
        "DB_Set_Tank_Mode. Неисправность. Давление выше заданного")]
    public void ByPlcTag_ReturnsExpectedError(string plcTag, string expectedCode, string expectedDescription)
    {
        var error = ErrorDefinitions.ByPlcTag(plcTag);

        Assert.NotNull(error);
        Assert.Equal(expectedCode, error.Code);
        Assert.Equal(expectedDescription, error.Description);
    }

    [Fact]
    public void ChStartMaxHeatoutWithoutPlcError_BindsToSeparateWithoutStep()
    {
        var error = ErrorDefinitions.AlNoWaterFlowChStartMaxHeatoutWithout;

        Assert.Equal("coms-ch-start-max-heatout-without", error.RelatedStepId);
        Assert.Equal("Coms/CH_Start_Max_Heatout_Without", error.RelatedStepName);
    }

    private static IEnumerable<(string Name, ErrorDefinition Definition)> GetDeclaredDefinitions()
    {
        return typeof(ErrorDefinitions)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(ErrorDefinition))
            .Select(field => (field.Name, (ErrorDefinition)field.GetValue(null)!));
    }
}

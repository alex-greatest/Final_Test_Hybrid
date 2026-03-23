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

    private static IEnumerable<(string Name, ErrorDefinition Definition)> GetDeclaredDefinitions()
    {
        return typeof(ErrorDefinitions)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(ErrorDefinition))
            .Select(field => (field.Name, (ErrorDefinition)field.GetValue(null)!));
    }
}

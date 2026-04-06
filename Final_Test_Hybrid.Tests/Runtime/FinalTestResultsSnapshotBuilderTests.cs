using Final_Test_Hybrid.Models;
using Final_Test_Hybrid.Models.Errors;
using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Services.Errors;
using Final_Test_Hybrid.Services.Results;
using Final_Test_Hybrid.Services.SpringBoot.Operator;
using Final_Test_Hybrid.Services.SpringBoot.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Timing;
using Final_Test_Hybrid.Services.Storage;
using Final_Test_Hybrid.Settings.App;
using Final_Test_Hybrid.Tests.TestSupport;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Tests.Runtime;

public class FinalTestResultsSnapshotBuilderTests
{
    [Fact]
    public void TryBuild_ReturnsExpectedRuntimeSnapshot()
    {
        var appSettings = CreateAppSettings();
        var boilerState = CreateBoilerState(appSettings);
        var operatorState = new OperatorState();
        var testResults = new TestResultsService();
        var errorService = new HistoryErrorService(["E1001", "E1002"]);
        var stepTimingService = new StepTimingService();

        boilerState.SetData("SN-001", "ART-1", true);
        operatorState.SetManualAuth("operator-1");
        testResults.Add("Voltage", "220", "", "", 1, false, "V", "Step A");
        testResults.Add("Timer_1", "00:00:12", "", "", 1, false, "", "Test Time");
        testResults.Add("Pressure", "1.40", "1.20", "1.60", 2, true, "bar", "Step B");
        stepTimingService.AddCompletedStepTiming("Step A", "desc", TimeSpan.FromSeconds(12));

        var builder = new FinalTestResultsSnapshotBuilder(
            boilerState,
            operatorState,
            appSettings,
            testResults,
            errorService,
            stepTimingService,
            TestInfrastructure.CreateDualLogger<FinalTestResultsSnapshotBuilder>());

        var built = builder.TryBuild(InterruptedTestResultCodes.Interrupted, out var snapshot, out var errorMessage);

        Assert.True(built);
        Assert.Equal(string.Empty, errorMessage);
        Assert.NotNull(snapshot);
        Assert.Equal("SN-001", snapshot.SerialNumber);
        Assert.Equal("ST-01", snapshot.StationName);
        Assert.Equal("operator-1", snapshot.Operator);
        Assert.Equal(InterruptedTestResultCodes.Interrupted, snapshot.Result);
        Assert.Equal("Voltage", snapshot.Items[0].Name);
        Assert.Equal("real", snapshot.Items[0].ValueType);
        Assert.Equal("Step A", snapshot.Items[0].Test);
        Assert.Equal("Timer_1", snapshot.Items[1].Name);
        Assert.Equal("string", snapshot.Items[1].ValueType);
        Assert.Equal("Test Time", snapshot.Items[1].Test);
        Assert.Equal("Pressure", snapshot.ItemsLimited[0].Name);
        Assert.Equal("1.20", snapshot.ItemsLimited[0].Min);
        Assert.Equal("1.60", snapshot.ItemsLimited[0].Max);
        Assert.Equal("Step B", snapshot.ItemsLimited[0].Test);
        Assert.Equal("Step A", snapshot.Time[0].Test);
        Assert.Equal("00.12", snapshot.Time[0].Time);
        Assert.Equal(["E1001", "E1002"], snapshot.Errors);
    }

    private static AppSettingsService CreateAppSettings()
    {
        return new AppSettingsService(Options.Create(new AppSettings
        {
            NameStation = "ST-01"
        }));
    }

    private static BoilerState CreateBoilerState(AppSettingsService appSettings)
    {
        return new BoilerState(appSettings, new TestRecipeProvider());
    }

    private sealed class HistoryErrorService(IReadOnlyList<string> codes) : IErrorService
    {
        public event Action? OnActiveErrorsChanged
        {
            add { }
            remove { }
        }

        public event Action? OnHistoryChanged
        {
            add { }
            remove { }
        }

        public bool HasResettableErrors => false;
        public bool HasActiveErrors => false;
        public bool IsHistoryEnabled { get; set; }

        public IReadOnlyList<ActiveError> GetActiveErrors()
        {
            return [];
        }

        public IReadOnlyList<ErrorHistoryItem> GetHistory()
        {
            return codes
                .Select(code => new ErrorHistoryItem
                {
                    Code = code,
                    Description = code,
                    StartTime = DateTime.UtcNow
                })
                .ToList();
        }

        public void Raise(ErrorDefinition error, string? details = null)
        {
        }

        public void RaiseInStep(ErrorDefinition error, string stepId, string stepName, string? details = null)
        {
        }

        public void Clear(string errorCode)
        {
        }

        public void ClearActiveApplicationErrors()
        {
        }

        public void RaisePlc(ErrorDefinition error, string? stepId = null, string? stepName = null)
        {
        }

        public void ClearPlc(string errorCode)
        {
        }

        public void ClearAllActiveErrors()
        {
        }

        public void ClearHistory()
        {
        }
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

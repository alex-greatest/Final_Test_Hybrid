using System.Net;
using System.Text;
using System.Text.Json;
using Final_Test_Hybrid.Models;
using Final_Test_Hybrid.Models.Errors;
using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Services.Errors;
using Final_Test_Hybrid.Services.Results;
using Final_Test_Hybrid.Services.SpringBoot.Operation.Interrupt;
using Final_Test_Hybrid.Services.SpringBoot.Operator;
using Final_Test_Hybrid.Services.SpringBoot.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Timing;
using Final_Test_Hybrid.Services.Storage;
using Final_Test_Hybrid.Settings.App;
using Final_Test_Hybrid.Settings.Spring;
using Final_Test_Hybrid.Tests.TestSupport;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Tests.Runtime;

public class InterruptedOperationServiceTests
{
    [Fact]
    public async Task SendAsync_PostsInterruptPayloadWithFinalResults()
    {
        var appSettings = new AppSettingsService(Options.Create(new AppSettings
        {
            NameStation = "ST-01"
        }));
        var boilerState = new BoilerState(appSettings, new TestRecipeProvider());
        var operatorState = new OperatorState();
        var testResults = new TestResultsService();
        var errorService = new HistoryErrorService(["E1001"]);
        var stepTimingService = new StepTimingService();
        var handler = new CapturingHandler();
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost")
        };
        var snapshotBuilder = new FinalTestResultsSnapshotBuilder(
            boilerState,
            operatorState,
            appSettings,
            testResults,
            errorService,
            stepTimingService,
            TestInfrastructure.CreateDualLogger<FinalTestResultsSnapshotBuilder>());
        var service = new InterruptedOperationService(
            new SpringBootHttpClient(httpClient, TestInfrastructure.CreateLogger<SpringBootHttpClient>()),
            appSettings,
            snapshotBuilder,
            TestInfrastructure.CreateDualLogger<InterruptedOperationService>());

        boilerState.SetData("SN-001", "ART-1", true);
        operatorState.SetManualAuth("operator-1");
        testResults.Add("Voltage", "220", "", "", 1, false, "V", "Step A");
        testResults.Add("Pressure", "1.40", "1.20", "1.60", 2, true, "bar", "Step B");
        stepTimingService.AddCompletedStepTiming("Step A", "desc", TimeSpan.FromSeconds(8));

        var result = await service.SendAsync("SN-001", "admin-1", "interrupt reason", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(handler.RequestBody);

        using var json = JsonDocument.Parse(handler.RequestBody);
        Assert.Equal("SN-001", json.RootElement.GetProperty("serialNumber").GetString());
        Assert.Equal("ST-01", json.RootElement.GetProperty("stationName").GetString());
        Assert.Equal("interrupt reason", json.RootElement.GetProperty("message").GetString());
        Assert.Equal("admin-1", json.RootElement.GetProperty("adminInterrupted").GetString());
        Assert.Equal("operator-1", json.RootElement.GetProperty("operator").GetString());
        Assert.Equal(InterruptedTestResultCodes.Interrupted, json.RootElement.GetProperty("result").GetInt32());
        Assert.Single(json.RootElement.GetProperty("Items").EnumerateArray());
        Assert.Single(json.RootElement.GetProperty("Items_limited").EnumerateArray());
        Assert.Single(json.RootElement.GetProperty("time").EnumerateArray());
        Assert.Single(json.RootElement.GetProperty("errors").EnumerateArray());
        Assert.False(json.RootElement.TryGetProperty("finalTestResults", out _));
    }

    [Fact]
    public async Task SendAsync_SendsReasonOnlyWhenSnapshotCannotBeBuilt()
    {
        var appSettings = new AppSettingsService(Options.Create(new AppSettings
        {
            NameStation = "ST-01"
        }));
        var boilerState = new BoilerState(appSettings, new TestRecipeProvider());
        var operatorState = new OperatorState();
        var testResults = new TestResultsService();
        var errorService = new HistoryErrorService(["E1001"]);
        var stepTimingService = new StepTimingService();
        var handler = new CapturingHandler();
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost")
        };
        var snapshotBuilder = new FinalTestResultsSnapshotBuilder(
            boilerState,
            operatorState,
            appSettings,
            testResults,
            errorService,
            stepTimingService,
            TestInfrastructure.CreateDualLogger<FinalTestResultsSnapshotBuilder>());
        var service = new InterruptedOperationService(
            new SpringBootHttpClient(httpClient, TestInfrastructure.CreateLogger<SpringBootHttpClient>()),
            appSettings,
            snapshotBuilder,
            TestInfrastructure.CreateDualLogger<InterruptedOperationService>());

        boilerState.SetData("SN-001", "ART-1", true);

        var result = await service.SendAsync("SN-001", "admin-1", "interrupt reason", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(handler.RequestBody);

        using var json = JsonDocument.Parse(handler.RequestBody);
        Assert.Equal("SN-001", json.RootElement.GetProperty("serialNumber").GetString());
        Assert.Equal("ST-01", json.RootElement.GetProperty("stationName").GetString());
        Assert.Equal("interrupt reason", json.RootElement.GetProperty("message").GetString());
        Assert.Equal("admin-1", json.RootElement.GetProperty("adminInterrupted").GetString());
        AssertPropertyMissingOrNull(json.RootElement, "operator");
        AssertPropertyMissingOrNull(json.RootElement, "Items");
        AssertPropertyMissingOrNull(json.RootElement, "Items_limited");
        AssertPropertyMissingOrNull(json.RootElement, "time");
        AssertPropertyMissingOrNull(json.RootElement, "errors");
        AssertPropertyMissingOrNull(json.RootElement, "result");
    }

    private static void AssertPropertyMissingOrNull(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return;
        }

        Assert.Equal(JsonValueKind.Null, property.ValueKind);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
            };
        }
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

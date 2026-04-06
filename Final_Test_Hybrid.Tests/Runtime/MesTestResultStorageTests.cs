using System.Net;
using System.Text;
using System.Text.Json;
using Final_Test_Hybrid.Models;
using Final_Test_Hybrid.Models.Errors;
using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Services.Errors;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Results;
using Final_Test_Hybrid.Services.SpringBoot.Operation.Finish;
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

public class MesTestResultStorageTests
{
    [Fact]
    public async Task SaveAsync_PostsFinishPayloadBuiltFromRuntimeSnapshot()
    {
        var appSettings = new AppSettingsService(Options.Create(new AppSettings
        {
            NameStation = "ST-01"
        }));
        var boilerState = new BoilerState(appSettings, new TestRecipeProvider());
        var operatorState = new OperatorState();
        var orderState = new OrderState(appSettings);
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
        var finishService = new OperationFinishService(
            new SpringBootHttpClient(httpClient, TestInfrastructure.CreateLogger<SpringBootHttpClient>()),
            orderState,
            TestInfrastructure.CreateDualLogger<OperationFinishService>());
        var storage = new MesTestResultStorage(
            boilerState,
            snapshotBuilder,
            finishService,
            TestInfrastructure.CreateDualLogger<MesTestResultStorage>());

        boilerState.SetData("SN-001", "ART-1", true);
        operatorState.SetManualAuth("operator-1");
        testResults.Add("Voltage", "220", "", "", 1, false, "V", "Step A");
        testResults.Add("Pressure", "1.40", "1.20", "1.60", 2, true, "bar", "Step B");
        stepTimingService.AddCompletedStepTiming("Step A", "desc", TimeSpan.FromSeconds(8));

        var result = await storage.SaveAsync(1, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("/api/operation/finish", handler.RequestPath);
        Assert.NotNull(handler.RequestBody);
        Assert.Equal(42, orderState.OrderNumber);
        Assert.Equal(100, orderState.AmountBoilerOrder);
        Assert.Equal(5, orderState.AmountBoilerMadeOrder);

        using var json = JsonDocument.Parse(handler.RequestBody);
        Assert.Equal("SN-001", json.RootElement.GetProperty("serialNumber").GetString());
        Assert.Equal("ST-01", json.RootElement.GetProperty("stationName").GetString());
        Assert.Equal("operator-1", json.RootElement.GetProperty("operator").GetString());
        Assert.Equal(1, json.RootElement.GetProperty("result").GetInt32());
        Assert.Single(json.RootElement.GetProperty("Items").EnumerateArray());
        Assert.Single(json.RootElement.GetProperty("Items_limited").EnumerateArray());
        Assert.Single(json.RootElement.GetProperty("time").EnumerateArray());
        Assert.Single(json.RootElement.GetProperty("errors").EnumerateArray());
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }

        public string? RequestPath { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestPath = request.RequestUri?.AbsolutePath;
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"orderNumber\":42,\"amountBoilerOrder\":100,\"amountBoilerMadeOrder\":5}",
                    Encoding.UTF8,
                    "application/json")
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

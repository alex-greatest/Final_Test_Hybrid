using Final_Test_Hybrid.Models.Results;
using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Final_Test_Hybrid.Services.Diagnostic.Protocol;
using Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;
using Final_Test_Hybrid.Services.Results;
using Final_Test_Hybrid.Services.SpringBoot.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Final_Test_Hybrid.Services.Steps.Steps.Coms;
using Final_Test_Hybrid.Tests.TestSupport;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class SafetyTimeStepTests
{
    private const string SafetyTimeMinRecipe = "ns=3;s=\"DB_Recipe\".\"Time\".\"ignSafetyTimeMin\"";
    private const string SafetyTimeMaxRecipe = "ns=3;s=\"DB_Recipe\".\"Time\".\"ignSafetyTimeMax\"";
    private const string ResultParameterName = "Safety time";

    [Fact]
    public async Task ExecuteAsync_PassesAndResetsBlockage_WhenModbusResponsesAreValid()
    {
        var client = new ScriptedModbusClient();
        client.EnqueueRead(1022, 250, 0, 0, 0, 0, 250);
        client.EnqueueRead(1022, 0, 0, 0, 0, 0, 0);
        client.EnqueueRead(1004, 1);

        var results = new TestResultsServiceStub();
        var step = CreateStep(results);
        var context = CreateContext(client, CreateRecipeProvider());

        var result = await step.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.EndsWith(" сек", result.Message);
        var saved = Assert.Single(results.GetResults());
        Assert.Equal(ResultParameterName, saved.ParameterName);
        Assert.Equal("Coms/Safety_Time", saved.Test);
        Assert.Equal("0.00", saved.Value.Replace(',', '.'));
        var write = Assert.Single(client.SingleWrites);
        Assert.Equal((ushort)1152, write.Address);
        Assert.Equal((ushort)0, write.Value);
    }

    [Fact]
    public async Task ExecuteAsync_Fails_WhenReadFailsWhileWaitingForCoils()
    {
        var client = new ScriptedModbusClient();
        client.EnqueueReadException(1022, 6, new TimeoutException("read timeout"));

        var results = new TestResultsServiceStub();
        var step = CreateStep(results);
        var context = CreateContext(client, CreateRecipeProvider());

        var result = await step.ExecuteAsync(context, CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(result.CanSkip);
        Assert.Contains("Ошибка чтения тока катушек", result.Message);
        Assert.Contains("Ошибка связи при чтении тока катушки EV1", result.Message);
        AssertCommunicationFailureResult(results);
    }

    [Fact]
    public async Task ExecuteAsync_Fails_WhenReadFailsDuringMeasurement()
    {
        var client = new ScriptedModbusClient();
        client.EnqueueRead(1022, 250, 0, 0, 0, 0, 250);
        client.EnqueueReadException(1022, 6, new TimeoutException("measure timeout"));

        var results = new TestResultsServiceStub();
        var step = CreateStep(results);
        var context = CreateContext(client, CreateRecipeProvider());

        var result = await step.ExecuteAsync(context, CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(result.CanSkip);
        Assert.Contains("Ошибка чтения тока катушек", result.Message);
        Assert.Contains("Ошибка связи при чтении тока катушки EV1", result.Message);
        AssertCommunicationFailureResult(results);
    }

    [Fact]
    public async Task ExecuteAsync_FailsWithoutSkip_WhenDispatcherIsNotStarted()
    {
        var client = new ScriptedModbusClient();
        client.EnqueueReadException(1022, 6, new InvalidOperationException("Диспетчер не запущен. Вызовите StartAsync() перед выполнением операций."));

        var results = new TestResultsServiceStub();
        var step = CreateStep(results);
        var context = CreateContext(client, CreateRecipeProvider());

        var result = await step.ExecuteAsync(context, CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(result.CanSkip);
        Assert.Contains("Ошибка связи при чтении тока катушки EV1", result.Message);
        AssertCommunicationFailureResult(results);
    }

    [Fact]
    public async Task ExecuteAsync_FailsWithoutSkip_WhenBlockageResetWriteFails()
    {
        var client = new ScriptedModbusClient();
        client.EnqueueRead(1022, 250, 0, 0, 0, 0, 250);
        client.EnqueueRead(1022, 0, 0, 0, 0, 0, 0);
        client.EnqueueRead(1004, 1);
        client.EnqueueWriteException(new TimeoutException("reset timeout"));

        var results = new TestResultsServiceStub();
        var step = CreateStep(results);
        var context = CreateContext(client, CreateRecipeProvider());

        var result = await step.ExecuteAsync(context, CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(result.CanSkip);
        Assert.Contains("Ошибка связи при сбросе блокировки", result.Message);
        var saved = Assert.Single(results.GetResults());
        Assert.Equal(ResultParameterName, saved.ParameterName);
        Assert.Equal("Coms/Safety_Time", saved.Test);
        Assert.Equal("0.00", saved.Value.Replace(',', '.'));
        Assert.Equal(1, saved.Status);
    }

    private static SafetyTimeStep CreateStep(TestResultsServiceStub results)
    {
        return new SafetyTimeStep(
            Options.Create(new DiagnosticSettings { BaseAddressOffset = 1 }),
            results,
            TestInfrastructure.CreateDualLogger<SafetyTimeStep>());
    }

    private static TestStepContext CreateContext(
        ScriptedModbusClient client,
        RecipeProviderStub recipeProvider)
    {
        var pauseToken = new PauseTokenSource();
        var reader = new RegisterReader(client, TestInfrastructure.CreateLogger<RegisterReader>(), new TestStepLoggerStub());
        var writer = new RegisterWriter(client, TestInfrastructure.CreateLogger<RegisterWriter>(), new TestStepLoggerStub());

        return new TestStepContext(
            columnIndex: 0,
            stepPacingWindow: TimeSpan.Zero,
            opcUa: null!,
            logger: TestInfrastructure.CreateLogger<TestStepContext>(),
            recipeProvider: recipeProvider,
            pauseToken: pauseToken,
            diagReader: new PausableRegisterReader(reader, pauseToken),
            diagWriter: new PausableRegisterWriter(writer, pauseToken),
            tagWaiter: null!,
            rangeSliderUiState: null!);
    }

    private static RecipeProviderStub CreateRecipeProvider()
    {
        return new RecipeProviderStub(
        [
            new RecipeResponseDto { Address = SafetyTimeMinRecipe, Value = "0.00" },
            new RecipeResponseDto { Address = SafetyTimeMaxRecipe, Value = "10.00" }
        ]);
    }

    private static void AssertCommunicationFailureResult(TestResultsServiceStub results)
    {
        var saved = Assert.Single(results.GetResults());
        Assert.Equal(ResultParameterName, saved.ParameterName);
        Assert.Equal("Coms/Safety_Time", saved.Test);
        Assert.Equal("0.00", saved.Value.Replace(',', '.'));
        Assert.Equal("0.00", saved.Min.Replace(',', '.'));
        Assert.Equal("10.00", saved.Max.Replace(',', '.'));
        Assert.Equal(2, saved.Status);
        Assert.True(saved.IsRanged);
        Assert.Equal("сек", saved.Unit);
    }

    private sealed class ScriptedModbusClient : IModbusClient
    {
        private readonly Queue<ReadScriptEntry> _reads = new();
        private readonly Queue<Exception> _writeExceptions = new();

        public List<(ushort Address, ushort Value)> SingleWrites { get; } = [];

        public void EnqueueRead(ushort address, params ushort[] values)
        {
            _reads.Enqueue(new ReadScriptEntry(address, values, null));
        }

        public void EnqueueReadException(ushort address, ushort count, Exception exception)
        {
            _reads.Enqueue(new ReadScriptEntry(address, new ushort[count], exception));
        }

        public void EnqueueWriteException(Exception exception)
        {
            _writeExceptions.Enqueue(exception);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        public Task<ushort[]> ReadHoldingRegistersAsync(
            ushort address,
            ushort count,
            CommandPriority priority = CommandPriority.High,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var read = Assert.IsType<ReadScriptEntry>(_reads.Dequeue());
            Assert.Equal(address, read.Address);
            Assert.Equal((ushort)read.Values.Length, count);
            if (read.Exception != null)
            {
                throw read.Exception;
            }

            return Task.FromResult(read.Values);
        }

        public Task WriteSingleRegisterAsync(
            ushort address,
            ushort value,
            CommandPriority priority = CommandPriority.High,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (_writeExceptions.Count > 0)
            {
                throw _writeExceptions.Dequeue();
            }

            SingleWrites.Add((address, value));
            return Task.CompletedTask;
        }

        public Task WriteMultipleRegistersAsync(
            ushort address,
            ushort[] values,
            CommandPriority priority = CommandPriority.High,
            CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        private sealed record ReadScriptEntry(ushort Address, ushort[] Values, Exception? Exception);
    }

    private sealed class RecipeProviderStub(IReadOnlyList<RecipeResponseDto> recipes) : IRecipeProvider
    {
        private readonly Dictionary<string, RecipeResponseDto> _recipes = recipes.ToDictionary(x => x.Address, x => x);

        public RecipeResponseDto? GetByAddress(string address)
        {
            return _recipes.GetValueOrDefault(address);
        }

        public IReadOnlyList<RecipeResponseDto> GetAll()
        {
            return _recipes.Values.ToList();
        }

        public void SetRecipes(IReadOnlyList<RecipeResponseDto> recipes)
        {
            _recipes.Clear();
            foreach (var recipe in recipes)
            {
                _recipes[recipe.Address] = recipe;
            }
        }

        public void Clear()
        {
            _recipes.Clear();
        }

        public T? GetValue<T>(string address) where T : struct
        {
            if (!_recipes.TryGetValue(address, out var recipe))
            {
                return null;
            }

            var value = Convert.ChangeType(recipe.Value, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
            return (T?)value;
        }

        public string? GetStringValue(string address)
        {
            return _recipes.TryGetValue(address, out var recipe) ? recipe.Value : null;
        }
    }

    private sealed class TestResultsServiceStub : ITestResultsService
    {
        private readonly List<TestResultItem> _items = [];

        public event Action? OnChanged;

        public IReadOnlyList<TestResultItem> GetResults()
        {
            return _items.ToList();
        }

        public void Add(string parameterName, string value, string min, string max, int status, bool isRanged, string unit, string test)
        {
            _items.Add(new TestResultItem
            {
                ParameterName = parameterName,
                Value = value,
                Min = min,
                Max = max,
                Status = status,
                IsRanged = isRanged,
                Unit = unit,
                Test = test
            });
            OnChanged?.Invoke();
        }

        public void Remove(string parameterName)
        {
            _items.RemoveAll(x => x.ParameterName == parameterName);
            OnChanged?.Invoke();
        }

        public void Clear()
        {
            _items.Clear();
            OnChanged?.Invoke();
        }
    }
}

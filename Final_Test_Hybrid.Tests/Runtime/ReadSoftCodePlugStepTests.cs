using System.Globalization;
using Final_Test_Hybrid.Models;
using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Final_Test_Hybrid.Services.Diagnostic.Protocol;
using Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;
using Final_Test_Hybrid.Services.Results;
using Final_Test_Hybrid.Services.SpringBoot.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Final_Test_Hybrid.Services.Steps.Steps.Coms;
using Final_Test_Hybrid.Settings.App;
using Final_Test_Hybrid.Tests.TestSupport;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class ReadSoftCodePlugStepTests
{
    private const string Article = "1234567890";
    private const string EngP3Article = "ENGP3-ABC123";
    private const string ItelmaArticle = "ITELMA-XYZ99";
    private const string ProductionDate = "20260402";
    private const uint SupplierCode = 12345678;
    private const uint CounterNumber = 654321;

    [Fact]
    public async Task ExecuteAsync_SavesStringRegistersWithCorrectResultNames()
    {
        var recipeProvider = CreateRecipeProvider();
        var boilerState = CreateBoilerState(recipeProvider);
        var results = new TestResultsService();
        var step = CreateStep(boilerState, results);
        var client = CreateHappyPathClient();
        var context = CreateContext(client, recipeProvider);

        var result = await step.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        AssertResult(results, "Soft_Code_Plug", Article, 1);
        AssertResult(results, "Nomenclature_EngP3", EngP3Article, 1);
        AssertResult(results, "Nomenclature_ITELMA", ItelmaArticle, 1);
    }

    [Fact]
    public async Task ExecuteAsync_FailsOnSoftCodePlugMismatch_AndStoresReadValue()
    {
        var recipeProvider = CreateRecipeProvider();
        var boilerState = CreateBoilerState(recipeProvider);
        var results = new TestResultsService();
        var step = CreateStep(boilerState, results);
        var client = new DictionaryModbusClient();
        client.AddUInt16(1054, 1);
        client.AddString(1175, 14, "WRONG-CODE");
        var context = CreateContext(client, recipeProvider);

        var result = await step.ExecuteAsync(context, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Soft Code Plug", result.Message);

        var saved = Assert.Single(results.GetResults(), x => x.ParameterName == "Soft_Code_Plug");
        Assert.Equal("WRONG-CODE", saved.Value);
        Assert.Equal(2, saved.Status);
        Assert.DoesNotContain(results.GetResults(), x => x.ParameterName == "Nomenclature_EngP3");
    }

    [Fact]
    public async Task ExecuteAsync_ClearsPreviousStringResultsBeforeRetry()
    {
        var recipeProvider = CreateRecipeProvider();
        var boilerState = CreateBoilerState(recipeProvider);
        var results = new TestResultsService();
        results.Add("Soft_Code_Plug", "old-soft", "", "", 1, false, "", "Coms/Read_Soft_Code_Plug");
        results.Add("Nomenclature_EngP3", "old-engp3", "", "", 1, false, "", "Coms/Read_Soft_Code_Plug");
        results.Add("Nomenclature_ITELMA", "old-itelma", "", "", 1, false, "", "Coms/Read_Soft_Code_Plug");

        var step = CreateStep(boilerState, results);
        var client = CreateHappyPathClient();
        var context = CreateContext(client, recipeProvider);

        var result = await step.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(results.GetResults(), x => x.ParameterName == "Soft_Code_Plug");
        Assert.Single(results.GetResults(), x => x.ParameterName == "Nomenclature_EngP3");
        Assert.Single(results.GetResults(), x => x.ParameterName == "Nomenclature_ITELMA");
        AssertResult(results, "Soft_Code_Plug", Article, 1);
        AssertResult(results, "Nomenclature_EngP3", EngP3Article, 1);
        AssertResult(results, "Nomenclature_ITELMA", ItelmaArticle, 1);
    }

    private static ReadSoftCodePlugStep CreateStep(BoilerState boilerState, ITestResultsService results)
    {
        return new ReadSoftCodePlugStep(
            boilerState,
            Options.Create(new DiagnosticSettings { BaseAddressOffset = 1 }),
            results,
            TestInfrastructure.CreateDualLogger<ReadSoftCodePlugStep>());
    }

    private static BoilerState CreateBoilerState(IRecipeProvider recipeProvider)
    {
        var boilerState = new BoilerState(
            new AppSettingsService(Options.Create(new AppSettings())),
            recipeProvider);
        boilerState.SetData("barcode", Article, true);
        return boilerState;
    }

    private static TestStepContext CreateContext(DictionaryModbusClient client, IRecipeProvider recipeProvider)
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
        var recipes = new List<RecipeResponseDto>();
        var stub = new RecipeProviderStub([]);
        var temporaryState = CreateBoilerState(stub);
        var temporaryStep = CreateStep(temporaryState, new TestResultsService());

        foreach (var key in temporaryStep.RequiredRecipeAddresses.Distinct(StringComparer.Ordinal))
        {
            var value = key.Contains("Flow_Coefficient", StringComparison.Ordinal)
                ? "1.234"
                : "1";
            recipes.Add(new RecipeResponseDto { Address = key, Value = value });
        }

        return new RecipeProviderStub(recipes);
    }

    private static DictionaryModbusClient CreateHappyPathClient()
    {
        var client = new DictionaryModbusClient();
        client.AddUInt16(1054, 1);
        client.AddString(1175, 14, Article);
        client.AddUInt16(1002, 1);
        client.AddUInt16(1003, 1);
        client.AddUInt16(1004, 1);
        client.AddUInt16(1157, 1);
        client.AddUInt16(1050, 1);
        client.AddUInt16(1051, 1);
        client.AddUInt16(1053, 1);
        client.AddUInt16(1108, 1);
        client.AddUInt16(1109, 1);
        client.AddUInt16(1065, 1);
        client.AddUInt16(1030, 1);
        client.AddFloat(1171, 1.234f);
        client.AddUInt16(1161, 1);
        client.AddUInt16(1160, 1);
        client.AddUInt16(1031, 1);
        client.AddUInt16(1052, 1);
        client.AddString(1139, 14, EngP3Article);
        client.AddString(1182, 14, ItelmaArticle);
        client.AddString(1133, 8, ProductionDate);
        client.AddUInt32(1131, SupplierCode);
        client.AddUInt32(1137, CounterNumber);
        client.AddUInt16(1071, 1);
        return client;
    }

    private static void AssertResult(
        TestResultsService results,
        string parameterName,
        string expectedValue,
        int expectedStatus)
    {
        var item = Assert.Single(results.GetResults(), x => x.ParameterName == parameterName);
        Assert.Equal(expectedValue, item.Value);
        Assert.Equal(expectedStatus, item.Status);
        Assert.Equal("Coms/Read_Soft_Code_Plug", item.Test);
    }

    private sealed class DictionaryModbusClient : IModbusClient
    {
        private readonly Dictionary<(ushort Address, ushort Count), ushort[]> _reads = [];

        public void AddUInt16(ushort documentAddress, ushort value)
        {
            _reads[(ToModbusAddress(documentAddress), 1)] = [value];
        }

        public void AddUInt32(ushort documentAddress, uint value)
        {
            _reads[(ToModbusAddress(documentAddress), 2)] =
            [
                (ushort)(value >> 16),
                (ushort)(value & 0xFFFF)
            ];
        }

        public void AddFloat(ushort documentAddress, float value)
        {
            var bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            _reads[(ToModbusAddress(documentAddress), 2)] =
            [
                (ushort)((bytes[0] << 8) | bytes[1]),
                (ushort)((bytes[2] << 8) | bytes[3])
            ];
        }

        public void AddString(ushort documentAddress, int maxLength, string value)
        {
            var registerCount = (ushort)((maxLength + 1) / 2);
            var registers = new ushort[registerCount];
            for (var index = 0; index < registerCount; index++)
            {
                var charIndex = index * 2;
                var highChar = charIndex < value.Length ? value[charIndex] : '\0';
                var lowChar = charIndex + 1 < value.Length ? value[charIndex + 1] : '\0';
                registers[index] = (ushort)((highChar << 8) | lowChar);
            }

            _reads[(ToModbusAddress(documentAddress), registerCount)] = registers;
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
            if (_reads.TryGetValue((address, count), out var values))
            {
                return Task.FromResult(values);
            }

            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Нет scripted read для address={0}, count={1}",
                    address,
                    count));
        }

        public Task WriteSingleRegisterAsync(
            ushort address,
            ushort value,
            CommandPriority priority = CommandPriority.High,
            CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task WriteMultipleRegistersAsync(
            ushort address,
            ushort[] values,
            CommandPriority priority = CommandPriority.High,
            CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        private static ushort ToModbusAddress(ushort documentAddress)
        {
            return (ushort)(documentAddress - 1);
        }
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

            var value = Convert.ChangeType(recipe.Value, typeof(T), CultureInfo.InvariantCulture);
            return (T?)value;
        }

        public string? GetStringValue(string address)
        {
            return _recipes.TryGetValue(address, out var recipe) ? recipe.Value : null;
        }
    }
}

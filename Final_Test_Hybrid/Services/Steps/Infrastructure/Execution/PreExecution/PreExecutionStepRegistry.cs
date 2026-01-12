using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;

public class PreExecutionStepRegistry : IPreExecutionStepRegistry
{
    private readonly Dictionary<string, IPreExecutionStep> _stepsById;
    private readonly AppSettingsService _appSettings;
    private readonly ILogger<PreExecutionStepRegistry> _logger;

    private static readonly string[] MesStepOrder =
    [
        "scan-barcode-mes",
        "validate-barcode",
        "start-operation-mes",
        "load-test-sequence",
        "build-test-maps",
        "save-boiler-state",
        "resolve-test-maps",
        "validate-recipes",
        "initialize-database",
        "write-recipes-to-plc",
        "initialize-recipe-provider",
        "block-boiler-adapter"
    ];

    private static readonly string[] NonMesStepOrder =
    [
        "scan-barcode",
        "validate-barcode",
        "find-boiler-type",
        "load-recipes",
        "load-test-sequence",
        "build-test-maps",
        "save-boiler-state",
        "resolve-test-maps",
        "validate-recipes",
        "initialize-database",
        "write-recipes-to-plc",
        "initialize-recipe-provider",
        "block-boiler-adapter"
    ];

    public PreExecutionStepRegistry(
        IEnumerable<IPreExecutionStep> steps,
        AppSettingsService appSettings,
        ILogger<PreExecutionStepRegistry> logger)
    {
        _stepsById = steps.ToDictionary(s => s.Id);
        _appSettings = appSettings;
        _logger = logger;
        ValidateRequiredSteps();
    }

    public IReadOnlyList<IPreExecutionStep> GetOrderedSteps()
    {
        var stepIds = _appSettings.UseMes ? MesStepOrder : NonMesStepOrder;
        return stepIds.Select(id => _stepsById[id]).ToList();
    }

    private void ValidateRequiredSteps()
    {
        var requiredIds = _appSettings.UseMes ? MesStepOrder : NonMesStepOrder;
        var missing = requiredIds.Where(id => !_stepsById.ContainsKey(id)).ToList();
        if (missing.Count == 0)
        {
            return;
        }
        var message = $"Не найдены PreExecution шаги: {string.Join(", ", missing)}";
        _logger.LogError(message);
        throw new InvalidOperationException(message);
    }
}

using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Errors;
using Final_Test_Hybrid.Services.Results;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Main.PlcReset;
using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.Scanner;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Base;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Coordinator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorHandling;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Scanning;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Final_Test_Hybrid.Services.Steps.Steps;
using Final_Test_Hybrid.Services.Steps.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace Final_Test_Hybrid.Services.DependencyInjection;

public static class StepsServiceExtensions
{
    public static IServiceCollection AddStepsServices(this IServiceCollection services)
    {
        // Validation
        services.AddSingleton<RecipeTagValidator>();
        services.AddSingleton<RequiredTagValidator>();
        services.AddSingleton<RecipeValidator>();
        services.AddSingleton<PlcSubscriptionValidator>();
        services.AddSingleton<PlcSubscriptionState>();
        services.AddSingleton<PlcSubscriptionInitializer>();

        // Recipe
        services.AddSingleton<IRecipeProvider, RecipeProvider>();

        // Message service
        services.AddSingleton<MessageService>();
        services.AddSingleton<AutoReadySubscription>();
        services.AddSingleton<MessageServiceInitializer>();

        // Execution state
        services.AddSingleton<ExecutionActivityTracker>();
        services.AddSingleton<ExecutionMessageState>();
        services.AddSingleton<InterruptMessageState>();
        services.AddSingleton<ResetMessageState>();
        services.AddSingleton<ResetSubscription>();
        services.AddSingleton<PlcResetCoordinator>();

        // Test sequence
        services.AddSingleton<TestSequenseService>();
        services.AddSingleton<ITestSequenceLoader, TestSequenceLoader>();
        services.AddSingleton<ITestMapBuilder, TestMapBuilder>();
        services.AddSingleton<ITestMapResolver, TestMapResolver>();

        // State management
        services.AddSingleton<BoilerState>();
        services.AddSingleton<SettingsAccessStateManager>();
        services.AddSingleton<OrderState>();

        // Scanning
        services.AddSingleton<BarcodeScanService>();
        services.AddSingleton<ScanSessionManager>();
        services.AddSingleton<ScanStateManager>();
        services.AddSingleton<ScanErrorHandler>();
        services.AddSingleton<ScanDialogCoordinator>();
        services.AddSingleton<ScanModeController>();
        services.AddSingleton<ScanStepManager>();

        // Execution
        services.AddSingleton<ExecutionStateManager>();
        services.AddSingleton<StepStatusReporter>();
        services.AddSingleton<ErrorPlcMonitor>();
        services.AddSingleton<PauseTokenSource>();
        services.AddSingleton<ErrorCoordinator>();
        services.AddSingleton<TestExecutionCoordinator>();

        // Errors
        services.AddSingleton<IErrorService, ErrorService>();
        services.AddSingleton<IPlcErrorMonitorService, PlcErrorMonitorService>();

        // Results
        services.AddSingleton<ITestResultsService, TestResultsService>();

        // Test step registry
        services.AddSingleton<ITestStepRegistry, TestStepRegistry>();

        // Pre-execution
        services.AddSingleton<IPreExecutionStepRegistry, PreExecutionStepRegistry>();
        services.AddSingleton<IPreExecutionStep, ScanBarcodeStep>();
        services.AddSingleton<IPreExecutionStep, ScanBarcodeMesStep>();
        services.AddSingleton<IPreExecutionStep, WriteRecipesToPlcStep>();
        services.AddSingleton<IPreExecutionStep, ResolveTestMapsStep>();
        services.AddSingleton<IPreExecutionStep, ValidateRecipesStep>();
        services.AddSingleton<IPreExecutionStep, InitializeDatabaseStep>();
        services.AddSingleton<IPreExecutionStep, InitializeRecipeProviderStep>();
        services.AddSingleton<PreExecutionCoordinator>();

        return services;
    }
}

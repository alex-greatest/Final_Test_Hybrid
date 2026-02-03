using Final_Test_Hybrid.Models;
using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Diagnostic.Protocol;
using Final_Test_Hybrid.Services.Errors;
using Final_Test_Hybrid.Services.Export;
using Final_Test_Hybrid.Services.Results;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Main.Messages;
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
using Final_Test_Hybrid.Services.Steps.Infrastructure.Timing;
using Final_Test_Hybrid.Services.Steps.Steps;
using Final_Test_Hybrid.Services.Steps.Steps.Misc;
using Final_Test_Hybrid.Services.Steps.Validation;
using Final_Test_Hybrid.Services.Preparation;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator.Behaviors;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Completion;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Lifecycle;
using Final_Test_Hybrid.Services.SpringBoot.Operation.Interrupt;
using Final_Test_Hybrid.Services.Storage;
using Final_Test_Hybrid.Services.Storage.Interfaces;
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
        services.AddSingleton<PreExecutionPlcValidator>();
        services.AddSingleton<PlcSubscriptionState>();
        services.AddSingleton<PlcSubscriptionInitializer>();
        services.AddSingleton<PlcInitializationCoordinator>();

        // Recipe
        services.AddSingleton<IRecipeProvider, RecipeProvider>();

        // Message service
        services.AddSingleton<MessageService>();
        services.AddSingleton<AutoReadySubscription>();
        services.AddSingleton<IChangeoverStartGate, ChangeoverStartGate>();
        services.AddSingleton<MessageServiceInitializer>();

        // Execution state
        services.AddSingleton<ExecutionActivityTracker>();
        services.AddSingleton<ExecutionPhaseState>();
        services.AddSingleton<ExecutionFlowState>();
        services.AddSingleton<ResetSubscription>();
        services.AddSingleton<PlcResetCoordinator>();

        // System lifecycle (new two-level architecture)
        services.AddSingleton<SystemLifecycleManager>();

        // Test completion
        services.AddSingleton<TestCompletionUiState>();
        services.AddSingleton<RangeSliderUiState>();
        services.AddSingleton<TestCompletionDependencies>();
        services.AddSingleton<TestCompletionCoordinator>();

        // Storage
        services.AddSingleton<IOperationStorageService, OperationStorageService>();
        services.AddSingleton<IResultStorageService, ResultStorageService>();
        services.AddSingleton<IErrorStorageService, ErrorStorageService>();
        services.AddSingleton<IStepTimeStorageService, StepTimeStorageService>();
        services.AddSingleton<DatabaseTestResultStorage>();
        services.AddSingleton<MesTestResultStorage>();
        services.AddSingleton<TestResultStorageRouter>();
        services.AddSingleton<ITestResultStorage>(sp => sp.GetRequiredService<TestResultStorageRouter>());

        // Interrupt reason services (InterruptDialogService и InterruptFlowExecutor создаются per-request)
        services.AddSingleton<InterruptedOperationService>();
        services.AddSingleton<InterruptReasonStorageService>();
        services.AddSingleton<InterruptReasonRouter>();

        // Test sequence
        services.AddSingleton<StepHistoryService>();
        services.AddSingleton<TestSequenseService>();
        services.AddSingleton<StepHistoryExcelExporter>();
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
        services.AddSingleton<ScanErrorHandler>();
        services.AddSingleton<ScanDialogCoordinator>();
        services.AddSingleton<ScanModeController>();

        // Execution
        services.AddSingleton<ExecutionStateManager>();
        services.AddSingleton<StepStatusReporter>();
        services.AddSingleton<ErrorPlcMonitor>();
        services.AddSingleton<PauseTokenSource>();

        // Pausable Modbus сервисы (используются только в тестовых шагах)
        services.AddSingleton<PausableRegisterReader>();
        services.AddSingleton<PausableRegisterWriter>();

        // ErrorCoordinator dependency groups
        services.AddSingleton<ErrorCoordinatorSubscriptions>();
        services.AddSingleton<ErrorResolutionServices>();

        // ErrorCoordinator behaviors
        services.AddSingleton<IInterruptBehavior, PlcConnectionLostBehavior>();
        services.AddSingleton<IInterruptBehavior, AutoModeDisabledBehavior>();
        services.AddSingleton<IInterruptBehavior, TagTimeoutBehavior>();
        services.AddSingleton<InterruptBehaviorRegistry>();

        // ErrorCoordinator
        services.AddSingleton<ErrorCoordinator>();
        services.AddSingleton<IErrorCoordinator>(sp => sp.GetRequiredService<ErrorCoordinator>());

        services.AddSingleton<TestExecutionCoordinator>();
        services.AddSingleton<IStepTimingService, StepTimingService>();
        services.AddSingleton<ITimerService, TimerService>();

        // Errors
        services.AddSingleton<IErrorService, ErrorService>();
        services.AddSingleton<IPlcErrorMonitorService, PlcErrorMonitorService>();

        // Results
        services.AddSingleton<ITestResultsService, TestResultsService>();

        // Test step registry
        services.AddSingleton<ITestStepRegistry, TestStepRegistry>();

        // Preparation services
        services.AddSingleton<IBoilerDataLoader, BoilerDataLoader>();
        services.AddSingleton<IBoilerDatabaseInitializer, BoilerDatabaseInitializer>();
        services.AddSingleton<IScanPreparationFacade, ScanPreparationFacade>();

        // Pre-execution (упрощённая архитектура: 2 scan-шага + StartTimer1 + BlockBoilerAdapter)
        services.AddSingleton<ScanBarcodeStep>();
        services.AddSingleton<ScanBarcodeMesStep>();
        services.AddSingleton<StartTimer1Step>();
        services.AddSingleton<BlockBoilerAdapterStep>();
        services.AddSingleton<IPreExecutionStepRegistry, PreExecutionStepAdapter>();

        // Pre-execution dependencies
        services.AddSingleton<PreExecutionSteps>();
        services.AddSingleton<PreExecutionInfrastructure>();
        services.AddSingleton<PreExecutionCoordinators>();
        services.AddSingleton<PreExecutionState>();
        services.AddSingleton<PreExecutionCoordinator>();

        return services;
    }
}

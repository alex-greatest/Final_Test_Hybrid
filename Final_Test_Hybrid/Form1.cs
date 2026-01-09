using System.Globalization;
using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Database;
using Final_Test_Hybrid.Services.Database.Config;
using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.OpcUa.Connection;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Common.IO;
using Final_Test_Hybrid.Services.Common.UI;
using Final_Test_Hybrid.Services.Steps;
using Final_Test_Hybrid.Services.SpringBoot.Health;
using Final_Test_Hybrid.Services.SpringBoot.Operation;
using Final_Test_Hybrid.Services.SpringBoot.Recipe;
using Final_Test_Hybrid.Services.SpringBoot.ErrorSettings;
using Final_Test_Hybrid.Services.SpringBoot.ResultSettings;
using Final_Test_Hybrid.Services.SpringBoot.StepFinalTest;
using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Services.Editor;
using Final_Test_Hybrid.Services.SpringBoot.Operator;
using Final_Test_Hybrid.Services.SpringBoot.Shift;
using Final_Test_Hybrid.Settings.App;
using Final_Test_Hybrid.Settings.Database;
using Final_Test_Hybrid.Settings.OpcUa;
using Final_Test_Hybrid.Settings.Spring;
using Final_Test_Hybrid.Services.Scanner;
using Final_Test_Hybrid.Services.Scanner.RawInput;
using Final_Test_Hybrid.Services.Diagnostic.Access;
using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Final_Test_Hybrid.Services.Diagnostic.Polling;
using Final_Test_Hybrid.Services.Diagnostic.Protocol;
using Final_Test_Hybrid.Settings.Spring.Shift;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Main.PlcReset;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Base;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Coordinator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorHandling;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Scanning;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Final_Test_Hybrid.Services.Steps.Infrastructure;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.PreExecution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;
using Final_Test_Hybrid.Services.Steps.Steps;
using Final_Test_Hybrid.Services.Steps.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Radzen;
using Serilog;

namespace Final_Test_Hybrid
{
    public partial class Form1 : Form
    {
        private IConfiguration? _config;
        private readonly ServiceProvider? _serviceProvider;
        private OpcUaConnectionService? _opcUaService;
        private SpringBootHealthService? _springBootHealthService;
        private ShiftService? _shiftService;
        private RawInputService? _rawInputService;
        private RawInputMessageFilter? _rawInputMessageFilter;
        private DatabaseConnectionService? _databaseConnectionService;

        public Form1()
        {
            InitializeComponent();
            var services = new ServiceCollection();
            SettingConfiguration(services);
            SettingDevTools(services);
            services.AddScoped<IFilePickerService, WinFormsFilePickerService>();
            services.AddScoped<ISequenceExcelService, SequenceExcelService>();
            services.AddSingleton<BlazorDispatcherAccessor>();
            services.AddSingleton<IUiDispatcher, BlazorUiDispatcher>();
            services.AddSingleton<INotificationService, NotificationServiceWrapper>();
            services.AddScoped<TestSequenceService>();
            services.AddSingleton<ITestStepRegistry, TestStepRegistry>();
            services.Configure<AppSettings>(_config!.GetSection("Settings"));
            services.AddSingleton<AppSettingsService>();
            RegisterOpcUaServices(services);
            RegisterSpringBootServices(services);
            RegisterShiftServices(services);
            RegisterScannerServices(services);
            RegisterDatabaseServices(services);
            RegisterDiagnosticServices(services);
            services.AddSingleton<RecipeTagValidator>();
            services.AddSingleton<RequiredTagValidator>();
            services.AddSingleton<RecipeValidator>();
            services.AddSingleton<PlcSubscriptionValidator>();
            services.AddSingleton<PlcSubscriptionState>();
            services.AddSingleton<PlcSubscriptionInitializer>();
            services.AddSingleton<IRecipeProvider, RecipeProvider>();
            services.AddSingleton<MessageService>();
            services.AddSingleton<AutoReadySubscription>();
            services.AddSingleton<MessageServiceInitializer>();
            services.AddSingleton<ExecutionActivityTracker>();
            services.AddSingleton<ExecutionMessageState>();
            services.AddSingleton<InterruptMessageState>();
            services.AddSingleton<ResetMessageState>();
            services.AddSingleton<ResetSubscription>();
            services.AddSingleton<PlcResetCoordinator>();
            services.AddSingleton<TestSequenseService>();
            services.AddSingleton<BoilerState>();
            services.AddSingleton<SettingsAccessStateManager>();
            services.AddSingleton<OrderState>();
            services.AddSingleton<BarcodeScanService>();
            services.AddSingleton<ScanSessionManager>();
            services.AddSingleton<ScanStateManager>();
            services.AddSingleton<ScanErrorHandler>();
            services.AddSingleton<ScanDialogCoordinator>();
            services.AddSingleton<ScanModeController>();
            services.AddSingleton<ScanStepManager>();
            services.AddSingleton<ITestSequenceLoader, TestSequenceLoader>();
            services.AddSingleton<ITestMapBuilder, TestMapBuilder>();
            services.AddSingleton<ITestMapResolver, TestMapResolver>();
            services.AddSingleton<ExecutionStateManager>();
            services.AddSingleton<StepStatusReporter>();
            services.AddSingleton<ErrorPlcMonitor>();
            services.AddSingleton<PauseTokenSource>();
            services.AddSingleton<ErrorCoordinator>();
            services.AddSingleton<TestExecutionCoordinator>();
            services.AddSingleton<IPreExecutionStepRegistry, PreExecutionStepRegistry>();
            services.AddSingleton<IPreExecutionStep, ScanBarcodeStep>();
            services.AddSingleton<IPreExecutionStep, ScanBarcodeMesStep>();
            services.AddSingleton<IPreExecutionStep, WriteRecipesToPlcStep>();
            services.AddSingleton<IPreExecutionStep, ResolveTestMapsStep>();
            services.AddSingleton<IPreExecutionStep, ValidateRecipesStep>();
            services.AddSingleton<IPreExecutionStep, InitializeDatabaseStep>();
            services.AddSingleton<IPreExecutionStep, InitializeRecipeProviderStep>();
            services.AddSingleton<PreExecutionCoordinator>();
            services.AddBlazorWebViewDeveloperTools();
            services.AddWindowsFormsBlazorWebView();
            services.AddRadzenComponents();
            // Override Radzen's scoped NotificationService with singleton for hybrid app
            services.AddSingleton<Radzen.NotificationService>();
            blazorWebView1.HostPage = "wwwroot\\index.html";
            _serviceProvider = services.BuildServiceProvider();
            var logger = _serviceProvider.GetRequiredService<ILogger<Form1>>();
            HandleException(logger);
            blazorWebView1.Services = _serviceProvider;
            StartOpcUaConnection(_serviceProvider);
            StartSpringBootHealthCheck(_serviceProvider);
            StartShiftService(_serviceProvider);
            StartRawInputService(_serviceProvider);
            StartDatabaseService(_serviceProvider);
            StartMessageService(_serviceProvider);
            ConfigureDiagnosticEvents(_serviceProvider);
            _ = _serviceProvider.GetRequiredService<ScanStepManager>();
            blazorWebView1.RootComponents.Add<MyComponent>("#app");
        }

        private void StartRawInputService(ServiceProvider serviceProvider)
        {
            _rawInputService = serviceProvider.GetRequiredService<RawInputService>();
            Load += (_, _) =>
            {
                try
                {
                    _rawInputService.Register(Handle);
                    _rawInputMessageFilter = new RawInputMessageFilter(_rawInputService);
                    Application.AddMessageFilter(_rawInputMessageFilter);
                }
                catch (Exception ex)
                {
                    var logger = serviceProvider.GetService<ILogger<Form1>>();
                    logger?.LogError(ex, "Ошибка инициализации Raw Input");
                }
            };
        }

        private static void HandleException(Microsoft.Extensions.Logging.ILogger logger)
        {
            Application.ThreadException += (_, error) =>
            {
                logger.LogCritical(error.Exception, "Необработанное исключение в UI потоке");
                #if DEBUG
                    MessageBox.Show(text: error.Exception.ToString(), caption: @"Ошибка");
                #else
                    MessageBox.Show(text: "An error has occurred.", caption: "Error");
                #endif
                Environment.Exit(1);
            };
            AppDomain.CurrentDomain.UnhandledException += (_, error) =>
            {
                logger.LogCritical(error.ExceptionObject as Exception, "Необработанное исключение в AppDomain");
                #if DEBUG
                    MessageBox.Show(text: error.ExceptionObject.ToString(), caption: @"Ошибка");
                #else
                    MessageBox.Show(text: "An error has occurred.", caption: "Error");
                #endif
            };
        }
        
        private void SettingConfiguration(ServiceCollection services)
        {
            _config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();
            services.AddSingleton(_config);
        }
        
        private void SettingDevTools(ServiceCollection services)
        {
            #if DEBUG
                services.AddBlazorWebViewDeveloperTools();
            #endif
            ConfigureSerilog();
            services.AddSingleton<ISubscriptionLogger, SubscriptionLogger>();
            services.AddSingleton<IDatabaseLogger, DatabaseLogger>();
            services.AddSingleton<ISpringBootLogger, SpringBootLogger>();
            services.AddSingleton<ITestStepLogger, TestStepLogger>();
            services.AddTransient(typeof(DualLogger<>));
            var logLevel = Enum.Parse<LogLevel>(_config?["Logging:General:LogLevel"] ?? "Warning");
            services.AddLogging(logging =>
            {
                logging.SetMinimumLevel(logLevel);
                #if DEBUG
                    logging.AddDebug();
                    logging.AddConsole();
                #endif
                logging.AddSerilog(Log.Logger, dispose: true);
            });
        }

        private void ConfigureSerilog()
        {
            var logConfig = _config?.GetSection("Logging:General");
            var path = logConfig?["Path"] ?? "D:/Logs/app-.txt";
            var retain = int.Parse(logConfig?["RetainedFileCountLimit"] ?? "5", CultureInfo.InvariantCulture);
            var level = Enum.Parse<Serilog.Events.LogEventLevel>(logConfig?["LogLevel"] ?? "Warning");
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(level)
                .WriteTo.File(path, rollingInterval: RollingInterval.Day, retainedFileCountLimit: retain)
                .CreateLogger();
        }

        private void RegisterOpcUaServices(ServiceCollection services)
        {
            services.Configure<OpcUaSettings>(_config!.GetSection("OpcUa"));
            services.AddSingleton<OpcUaConnectionState>();
            services.AddSingleton<OpcUaSubscription>();
            services.AddSingleton<OpcUaConnectionService>();
            services.AddSingleton<OpcUaTagService>();
            services.AddSingleton<OpcUaBrowseService>();
            services.AddSingleton<PausableOpcUaTagService>();
            services.AddSingleton<TagWaiter>();
            services.AddSingleton<PausableTagWaiter>();
        }

        private void RegisterSpringBootServices(ServiceCollection services)
        {
            services.Configure<SpringBootSettings>(_config!.GetSection("SpringBoot"));
            services.AddSingleton<SpringBootConnectionState>();
            services.AddSingleton<SpringBootHttpClient>();
            services.AddSingleton<SpringBootHealthService>();
            services.AddSingleton<OperatorState>();
            services.AddSingleton<OperatorAuthService>();
            services.AddScoped<RecipeDownloadService>();
            services.AddScoped<ResultSettingsDownloadService>();
            services.AddScoped<ErrorSettingsDownloadService>();
            services.AddScoped<StepFinalTestDownloadService>();
            services.AddScoped<OperationStartService>();
            services.AddScoped<ReworkDialogService>();
        }

        private void RegisterShiftServices(ServiceCollection services)
        {
            services.Configure<ShiftSettings>(_config!.GetSection("Shift"));
            services.AddSingleton<ShiftState>();
            services.AddSingleton<ShiftService>();
        }

        private void RegisterScannerServices(ServiceCollection services)
        {
            services.AddSingleton<ScannerConnectionState>();
            services.AddSingleton<RawInputService>();
        }

        private void RegisterDatabaseServices(ServiceCollection services)
        {
            var dbSettings = _config!.GetSection("Database").Get<DatabaseSettings>();
            services.Configure<DatabaseSettings>(_config!.GetSection("Database"));
            services.AddDbContextFactory<AppDbContext>(options =>
            {
                options.UseNpgsql(dbSettings?.ConnectionString ?? string.Empty, npgsqlOptions =>
                {
                    npgsqlOptions.CommandTimeout(dbSettings?.ConnectionTimeoutSeconds ?? 5);
                });
            });
            services.AddSingleton<DatabaseConnectionState>();
            services.AddSingleton<DatabaseConnectionService>();
            services.AddScoped<BoilerTypeService>();
            services.AddScoped<BoilerService>();
            services.AddScoped<OperationService>();
            services.AddScoped<RecipeService>();
            services.AddScoped<ResultSettingsService>();
            services.AddScoped<StepFinalTestService>();
            services.AddScoped<ErrorSettingsTemplateService>();
        }

        private void RegisterDiagnosticServices(ServiceCollection services)
        {
            services.Configure<DiagnosticSettings>(_config!.GetSection("Diagnostic"));
            services.AddSingleton<DiagnosticConnectionState>();
            services.AddSingleton<DiagnosticConnectionService>();
            services.AddSingleton<PollingPauseCoordinator>();
            services.AddSingleton<ModbusClient>();
            services.AddSingleton<RegisterReader>();
            services.AddSingleton<RegisterWriter>();
            services.AddSingleton<AccessLevelManager>();
            services.AddSingleton<PollingService>();
        }

        private void StartDatabaseService(ServiceProvider serviceProvider)
        {
            _databaseConnectionService = serviceProvider.GetRequiredService<DatabaseConnectionService>();
            _databaseConnectionService.Start();
        }

        private static void ConfigureDiagnosticEvents(ServiceProvider serviceProvider)
        {
            var connectionService = serviceProvider.GetRequiredService<DiagnosticConnectionService>();
            var pollingService = serviceProvider.GetRequiredService<PollingService>();

            connectionService.Disconnecting += () => pollingService.StopAllTasksAsync();
        }

        // async void намеренно: исключения должны попадать в Application.ThreadException
        // ReSharper disable once AsyncVoidMethod
        private static async void StartMessageService(ServiceProvider serviceProvider)
        {
            var initializer = serviceProvider.GetRequiredService<MessageServiceInitializer>();
            await initializer.InitializeAsync();

            var messageService = serviceProvider.GetRequiredService<MessageService>();
            var executionState = serviceProvider.GetRequiredService<ExecutionMessageState>();
            var interruptState = serviceProvider.GetRequiredService<InterruptMessageState>();
            var resetState = serviceProvider.GetRequiredService<ResetMessageState>();

            messageService.RegisterProvider(110, executionState.GetMessage);
            messageService.RegisterProvider(120, interruptState.GetMessage);
            messageService.RegisterProvider(130, resetState.GetMessage);

            executionState.OnChange += messageService.NotifyChanged;
            interruptState.OnChange += messageService.NotifyChanged;
            resetState.OnChange += messageService.NotifyChanged;

            // Инициализация ResetSubscription
            var resetSubscription = serviceProvider.GetRequiredService<ResetSubscription>();
            await resetSubscription.SubscribeAsync();

            // Создаём PlcResetCoordinator (подписка в конструкторе)
            _ = serviceProvider.GetRequiredService<PlcResetCoordinator>();
        }

        private void StartSpringBootHealthCheck(ServiceProvider serviceProvider)
        {
            _springBootHealthService = serviceProvider.GetRequiredService<SpringBootHealthService>();
            _springBootHealthService.Start();
        }

        private void StartShiftService(ServiceProvider serviceProvider)
        {
            _shiftService = serviceProvider.GetRequiredService<ShiftService>();
            _shiftService.Start();
        }

        // async void намеренно: исключения должны попадать в Application.ThreadException
        // ReSharper disable once AsyncVoidMethod
        private async void StartOpcUaConnection(ServiceProvider serviceProvider)
        {
            _opcUaService = serviceProvider.GetRequiredService<OpcUaConnectionService>();
            _opcUaService.ValidateSettings();
            await _opcUaService.ConnectAsync();

            // Проверка тегов обработки ошибок — если не удастся, приложение упадёт
            var errorPlcMonitor = serviceProvider.GetRequiredService<ErrorPlcMonitor>();
            await errorPlcMonitor.ValidateTagsAsync();

            // Подписка на теги всех шагов — если не удастся, приложение упадёт
            var subscriptionInitializer = serviceProvider.GetRequiredService<PlcSubscriptionInitializer>();
            await subscriptionInitializer.InitializeAsync();
        }

        protected override async void OnFormClosing(FormClosingEventArgs e)
        {
            try
            {
                if (_rawInputMessageFilter != null)
                {
                    Application.RemoveMessageFilter(_rawInputMessageFilter);
                    _rawInputMessageFilter = null;
                }
                _rawInputService?.Unregister();
                _springBootHealthService?.Stop();
                _shiftService?.Stop();
                _databaseConnectionService?.Stop();

                // Dispose PLC Reset components before OPC UA disconnection
                var plcResetCoordinator = _serviceProvider?.GetService<PlcResetCoordinator>();
                if (plcResetCoordinator != null)
                {
                    plcResetCoordinator.CancelCurrentReset();
                    await plcResetCoordinator.DisposeAsync();
                }

                var resetSubscription = _serviceProvider?.GetService<ResetSubscription>();
                if (resetSubscription != null)
                {
                    await resetSubscription.DisposeAsync();
                }

                if (_opcUaService != null)
                {
                    await _opcUaService.DisconnectAsync();
                }
                if (_serviceProvider != null)
                {
                    await _serviceProvider.DisposeAsync();
                }
                base.OnFormClosing(e);
            }
            catch (Exception)
            {
                //ignored
            }
        }
    }
}

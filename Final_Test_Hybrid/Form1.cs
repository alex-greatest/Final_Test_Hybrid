using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Database.Config;
using Final_Test_Hybrid.Services.DependencyInjection;
using Final_Test_Hybrid.Services.Diagnostic.Polling;
using Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Main.PlcReset;
using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.OpcUa.Connection;
using Final_Test_Hybrid.Services.OpcUa.Heartbeat;
using Final_Test_Hybrid.Services.Scanner.RawInput;
using Final_Test_Hybrid.Services.SpringBoot.Health;
using Final_Test_Hybrid.Services.SpringBoot.Shift;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Scanning;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Radzen;

namespace Final_Test_Hybrid;

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

        _config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton(_config);
        services.AddFinalTestServices(_config);

#if DEBUG
        services.AddBlazorWebViewDeveloperTools();
#endif
        services.AddWindowsFormsBlazorWebView();
        services.AddRadzenComponents();
        services.AddSingleton<NotificationService>(); // Override Radzen's scoped with singleton

        blazorWebView1.HostPage = "wwwroot\\index.html";
        _serviceProvider = services.BuildServiceProvider();

        var logger = _serviceProvider.GetRequiredService<ILogger<Form1>>();
        HandleException(logger);

        // Инициализация TestStepLogger при запуске приложения
        var testStepLogger = _serviceProvider.GetRequiredService<ITestStepLogger>();
        testStepLogger.StartNewSession();

        blazorWebView1.Services = _serviceProvider;
        StartServices(_serviceProvider);
        blazorWebView1.RootComponents.Add<MyComponent>("#app");
    }

    private void StartServices(ServiceProvider serviceProvider)
    {
        StartOpcUaConnection(serviceProvider);
        StartSpringBootHealthCheck(serviceProvider);
        StartShiftService(serviceProvider);
        StartRawInputService(serviceProvider);
        StartDatabaseService(serviceProvider);
        StartMessageService(serviceProvider);
        ConfigureDiagnosticEvents(serviceProvider);
        _ = serviceProvider.GetRequiredService<ScanModeController>();
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

    private void StartDatabaseService(ServiceProvider serviceProvider)
    {
        _databaseConnectionService = serviceProvider.GetRequiredService<DatabaseConnectionService>();
        _databaseConnectionService.Start();
    }

    private static void ConfigureDiagnosticEvents(ServiceProvider serviceProvider)
    {
        var pollingService = serviceProvider.GetRequiredService<PollingService>();
        var dispatcher = serviceProvider.GetRequiredService<IModbusDispatcher>();
        var plcResetCoordinator = serviceProvider.GetRequiredService<PlcResetCoordinator>();
        var errorCoordinator = serviceProvider.GetRequiredService<IErrorCoordinator>();
        var logger = serviceProvider.GetRequiredService<ILogger<Form1>>();

        // Подписываемся на Disconnecting для остановки polling
        dispatcher.Disconnecting += () => pollingService.StopAllTasksAsync();

        // Подписываемся на PLC Reset события для остановки диагностики
        plcResetCoordinator.OnForceStop += () => StopDispatcherSafely(dispatcher, logger);
        errorCoordinator.OnReset += () => StopDispatcherSafely(dispatcher, logger);

        // StartAsync() НЕ вызываем здесь — диагностика запускается из тестовых шагов
    }

    /// <summary>
    /// Безопасно останавливает диспетчер с логированием ошибок.
    /// </summary>
    private static void StopDispatcherSafely(IModbusDispatcher dispatcher, ILogger logger)
    {
        _ = dispatcher.StopAsync().ContinueWith(t =>
        {
            if (t is { IsFaulted: true, Exception: not null })
            {
                logger.LogError(t.Exception.GetBaseException(),
                    "Ошибка остановки диспетчера: {Error}",
                    t.Exception.GetBaseException().Message);
            }
        }, TaskScheduler.Default);
    }

    // async void намеренно: исключения должны попадать в Application.ThreadException
    // ReSharper disable once AsyncVoidMethod
    private static async void StartMessageService(ServiceProvider serviceProvider)
    {
        var initializer = serviceProvider.GetRequiredService<MessageServiceInitializer>();
        await initializer.InitializeAsync();

        // Initialize reset subscription and coordinator
        var resetSubscription = serviceProvider.GetRequiredService<ResetSubscription>();
        await resetSubscription.SubscribeAsync();

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

        // HMI Heartbeat — автоматически запустится после подключения
        _ = serviceProvider.GetRequiredService<HmiHeartbeatService>();

        // Координатор выполняет всю инициализацию PLC — если ошибка, приложение упадёт
        var coordinator = serviceProvider.GetRequiredService<PlcInitializationCoordinator>();
        await coordinator.InitializeAllAsync();
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

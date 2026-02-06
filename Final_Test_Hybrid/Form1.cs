using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Database.Config;
using Final_Test_Hybrid.Services.DependencyInjection;
using Final_Test_Hybrid.Services.Diagnostic.Polling;
using Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;
using Final_Test_Hybrid.Services.Diagnostic.Services;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Main.PlcReset;
using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.OpcUa.Connection;
using Final_Test_Hybrid.Services.OpcUa.Heartbeat;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
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
    private ScanModeController? _scanModeController;

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
        // ScanModeController создаётся в OnHandleCreated ПОСЛЕ регистрации Raw Input
    }

    private void StartRawInputService(ServiceProvider serviceProvider)
    {
        _rawInputService = serviceProvider.GetRequiredService<RawInputService>();
        // Если Handle уже создан (например, из-за BlazorWebView), регистрируем сразу
        if (IsHandleCreated)
        {
            RegisterRawInput();
            InitializeScanModeController();
        }
        // Иначе регистрация произойдёт в OnHandleCreated
    }

    /// <summary>
    /// Регистрирует Raw Input при создании Handle (до Form.Load).
    /// Обрабатывает пересоздание Handle (при смене DPI/стилей).
    /// </summary>
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        RegisterRawInput();
        InitializeScanModeController();
    }

    /// <summary>
    /// Отписывается от Raw Input при уничтожении Handle.
    /// </summary>
    protected override void OnHandleDestroyed(EventArgs e)
    {
        RemoveMessageFilter();
        UnregisterRawInput();
        base.OnHandleDestroyed(e);
    }

    /// <summary>
    /// Создаёт ScanModeController только после успешной регистрации Raw Input.
    /// </summary>
    private void InitializeScanModeController()
    {
        // Создаём только если Raw Input успешно зарегистрирован
        if (_rawInputMessageFilter == null || _scanModeController != null)
        {
            return;
        }
        _scanModeController = _serviceProvider?.GetRequiredService<ScanModeController>();
    }

    /// <summary>
    /// Регистрирует Raw Input и добавляет message filter.
    /// </summary>
    private void RegisterRawInput()
    {
        if (_rawInputService == null)
        {
            return;
        }
        try
        {
            var registered = _rawInputService.Register(Handle);
            if (!registered)
            {
                var logger = _serviceProvider?.GetService<ILogger<Form1>>();
                logger?.LogWarning("Raw Input registration returned false for handle {Handle}", Handle);
                return;
            }
            if (_rawInputMessageFilter == null)
            {
                _rawInputMessageFilter = new RawInputMessageFilter(_rawInputService);
                Application.AddMessageFilter(_rawInputMessageFilter);
            }
            var successLogger = _serviceProvider?.GetService<ILogger<Form1>>();
            successLogger?.LogInformation("Raw Input registered with handle {Handle}", Handle);
        }
        catch (Exception ex)
        {
            // Сбрасываем filter если AddMessageFilter бросил исключение
            _rawInputMessageFilter = null;
            var logger = _serviceProvider?.GetService<ILogger<Form1>>();
            logger?.LogError(ex, "Ошибка инициализации Raw Input");
        }
    }

    /// <summary>
    /// Удаляет message filter.
    /// </summary>
    private void RemoveMessageFilter()
    {
        if (_rawInputMessageFilter != null)
        {
            Application.RemoveMessageFilter(_rawInputMessageFilter);
            _rawInputMessageFilter = null;
        }
    }

    /// <summary>
    /// Отменяет регистрацию Raw Input.
    /// </summary>
    private void UnregisterRawInput()
    {
        _rawInputService?.Unregister();
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

        // ECU error sync — получаем чтобы создался и начал слушать события
        _ = serviceProvider.GetRequiredService<EcuErrorSyncService>();

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

        // Диагностика подписок — только логирование, без влияния на runtime-логику
        var subscriptionDiagnostics = serviceProvider.GetRequiredService<OpcUaSubscriptionDiagnosticsService>();
        await subscriptionDiagnostics.StartAsync();
    }

    protected override async void OnFormClosing(FormClosingEventArgs e)
    {
        try
        {
            RemoveMessageFilter();
            UnregisterRawInput();
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

            var subscriptionDiagnostics = _serviceProvider?.GetService<OpcUaSubscriptionDiagnosticsService>();
            if (subscriptionDiagnostics != null)
            {
                await subscriptionDiagnostics.StopAsync();
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

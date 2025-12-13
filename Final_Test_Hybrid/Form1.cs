using System.Globalization;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.OpcUa.Connection;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Final_Test_Hybrid.Services.Sequence;
using Final_Test_Hybrid.Services.Common.IO;
using Final_Test_Hybrid.Services.Common.UI;
using Final_Test_Hybrid.Services.Steps;
using Final_Test_Hybrid.Services.SpringBoot.Health;
using Final_Test_Hybrid.Services.Shift;
using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Settings.Shift;
using Final_Test_Hybrid.Settings;
using Final_Test_Hybrid.Settings.App;
using Final_Test_Hybrid.Settings.OpcUa;
using Final_Test_Hybrid.Settings.Spring;
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
        private OpcUaConnectionService? _opcUaService;
        private SpringBootHealthService? _springBootHealthService;
        private ShiftService? _shiftService;

        public Form1()
        {
            InitializeComponent();
            var services = new ServiceCollection();
            SettingConfiguration(services);
            SettingDevTools(services);
            services.AddScoped<IFilePickerService, WinFormsFilePickerService>();
            services.AddScoped<ISequenceExcelService, SequenceExcelService>();
            services.AddScoped<INotificationService, NotificationServiceWrapper>();
            services.AddScoped<TestSequenceService>();
            services.AddSingleton<ITestStepRegistry, TestStepRegistry>();
            services.Configure<AppSettings>(_config!.GetSection("Settings"));
            services.AddSingleton<AppSettingsService>();
            RegisterOpcUaServices(services);
            RegisterSpringBootServices(services);
            RegisterShiftServices(services);
            services.AddBlazorWebViewDeveloperTools();
            services.AddWindowsFormsBlazorWebView();
            services.AddRadzenComponents();
            blazorWebView1.HostPage = "wwwroot\\index.html";
            var serviceProvider = services.BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<ILogger<Form1>>();
            HandleException(logger);
            blazorWebView1.Services = serviceProvider;
            StartOpcUaConnection(serviceProvider);
            StartSpringBootHealthCheck(serviceProvider);
            StartShiftService(serviceProvider);
            blazorWebView1.RootComponents.Add<MyComponent>("#app");
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
        }

        private void RegisterSpringBootServices(ServiceCollection services)
        {
            services.Configure<SpringBootSettings>(_config!.GetSection("SpringBoot"));
            services.AddSingleton<SpringBootConnectionState>();
            services.AddSingleton<SpringBootHttpClient>();
            services.AddSingleton<SpringBootHealthService>();
        }

        private void RegisterShiftServices(ServiceCollection services)
        {
            services.Configure<ShiftSettings>(_config!.GetSection("Shift"));
            services.AddSingleton<ShiftState>();
            services.AddSingleton<ShiftService>();
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
        }

        protected override async void OnFormClosing(FormClosingEventArgs e)
        {
            _springBootHealthService?.Stop();
            _shiftService?.Stop();
            if (_opcUaService != null)
            {
                await _opcUaService.DisconnectAsync();
            }
            base.OnFormClosing(e);
        }
    }
}

using Final_Test_Hybrid.Models.Plc.Settings;
using Final_Test_Hybrid.Models.Plc.Tags;
using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Final_Test_Hybrid.Services.Sequence;
using Final_Test_Hybrid.Services.Settings.IO;
using Final_Test_Hybrid.Services.Settings.UI;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Radzen;

namespace Final_Test_Hybrid
{
    public partial class Form1 : Form
    {
        private IConfiguration? _config;
        private OpcUaConnectionService? _opcUaService;

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
            RegisterOpcUaServices(services);
            services.AddBlazorWebViewDeveloperTools();
            services.AddWindowsFormsBlazorWebView();
            services.AddRadzenComponents();
            blazorWebView1.HostPage = "wwwroot\\index.html";
            var serviceProvider = services.BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<ILogger<Form1>>();
            HandleException(logger);
            blazorWebView1.Services = serviceProvider;
            StartOpcUaConnection(serviceProvider);
            blazorWebView1.RootComponents.Add<MyComponent>("#app");
        }

        private static void HandleException(ILogger logger)
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
            services.AddSingleton<IConfiguration>(_config);
        }
        
        private void SettingDevTools(ServiceCollection services)
        {
            #if DEBUG
                services.AddBlazorWebViewDeveloperTools();
                services.AddLogging(logging =>
                {
                    logging.SetMinimumLevel(LogLevel.Information);
                    logging.AddDebug();
                    logging.AddConsole();
                    logging.AddFile(_config?.GetSection("Logging")!);
                });
            #else
                services.AddLogging(logging =>
                {
                    logging.SetMinimumLevel(LogLevel.Error);
                    logging.AddFile(_config?.GetSection("Logging"));
                });
            #endif
        }

        private void RegisterOpcUaServices(ServiceCollection services)
        {
            services.Configure<OpcUaSettings>(_config!.GetSection("OpcUa"));
            services.AddSingleton<OpcUaConnectionState>();
            services.AddSingleton<OpcUaSubscription>();
            services.AddSingleton<OpcUaConnectionService>();
            services.AddSingleton<OpcUaTagService>();
        }

        // async void намеренно: исключения должны попадать в Application.ThreadException
        // ReSharper disable once AsyncVoidMethod
        private async void StartOpcUaConnection(ServiceProvider serviceProvider)
        {
            _opcUaService = serviceProvider.GetRequiredService<OpcUaConnectionService>();
            _opcUaService.ValidateSettings();
            await _opcUaService.ConnectAsync();
            var subscription = serviceProvider.GetRequiredService<OpcUaSubscription>();
            var nodesToMonitor = GetNodesToMonitor();
            await subscription.AddTagsAsync(nodesToMonitor);
        }

        private static List<string> GetNodesToMonitor() =>
        [
            BaseTags.PcOn,
            BaseTags.Sb3011,
            BaseTags.PneuValveEv31AirOn
        ];

        protected override async void OnFormClosing(FormClosingEventArgs e)
        {
            if (_opcUaService != null)
            {
                await _opcUaService.DisconnectAsync();
            }
            base.OnFormClosing(e);
        }
    }
}

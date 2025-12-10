using Final_Test_Hybrid.Models.Plc.Settings;
using Final_Test_Hybrid.Services.OpcUa;
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
            HandleException();
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
            blazorWebView1.Services = serviceProvider;
            StartOpcUaConnection(serviceProvider);
            blazorWebView1.RootComponents.Add<MyComponent>("#app");
        }

        private static void HandleException()
        {
            AppDomain.CurrentDomain.UnhandledException += (_, error) =>
            {
                #if DEBUG
                    MessageBox.Show(text: error.ExceptionObject.ToString(), caption: @"������");
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
            services.AddSingleton<OpcUaConnectionService>();
        }

        private void StartOpcUaConnection(ServiceProvider serviceProvider)
        {
            _opcUaService = serviceProvider.GetRequiredService<OpcUaConnectionService>();
            _ = _opcUaService.ConnectAsync();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _opcUaService?.DisconnectAsync().GetAwaiter().GetResult();
            base.OnFormClosing(e);
        }
    }
}

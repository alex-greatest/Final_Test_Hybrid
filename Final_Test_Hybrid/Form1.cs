using Final_Test_Hybrid.Services;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Radzen;

namespace Final_Test_Hybrid
{
    public partial class Form1 : Form
    {
        private IConfiguration? _config;
        
        public Form1()
        {
            InitializeComponent();
            HandleException();
            var services = new ServiceCollection();
            SettingConfiguration(services);
            services.AddScoped<IFilePickerService, WinFormsFilePickerService>();
            services.AddScoped<TestSequenceService>();
            services.AddBlazorWebViewDeveloperTools();
            services.AddWindowsFormsBlazorWebView();
            services.AddRadzenComponents();
            blazorWebView1.HostPage = "wwwroot\\index.html";
            blazorWebView1.Services = services.BuildServiceProvider();
            //blazorWebView1.RootComponents.Add<Counter>("#app");
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
    }
}
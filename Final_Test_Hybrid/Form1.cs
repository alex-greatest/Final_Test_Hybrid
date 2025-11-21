using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.DependencyInjection;
using Radzen;

namespace Final_Test_Hybrid
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            HandleException();
            var services = new ServiceCollection();
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
    }
}
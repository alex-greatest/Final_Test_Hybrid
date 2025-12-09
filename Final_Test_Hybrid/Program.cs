using Opc.Ua;

namespace Final_Test_Hybrid
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            
            // Set EPPlus license context to NonCommercial using the static License property
            // This is required for EPPlus 8+
            OfficeOpenXml.ExcelPackage.License.SetNonCommercialPersonal("FinalTestHybrid");

            ApplicationConfiguration.Initialize();
            Application.Run(new Form1());
        }
    }
}
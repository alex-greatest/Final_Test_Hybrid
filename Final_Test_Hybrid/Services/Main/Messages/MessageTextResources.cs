namespace Final_Test_Hybrid.Services.Main.Messages;

using System.Globalization;
using System.Resources;

internal static class MessageTextResources
{
    private static readonly ResourceManager ResourceManager = new(
        "Final_Test_Hybrid.Form1",
        typeof(MessageTextResources).Assembly);

    internal static string PlcConnectionLostResetting => GetString(nameof(PlcConnectionLostResetting));
    internal static string TagTimeoutResetting => GetString(nameof(TagTimeoutResetting));
    internal static string PlcConnectionLostPendingReset => GetString(nameof(PlcConnectionLostPendingReset));
    internal static string TagTimeout => GetString(nameof(TagTimeout));
    internal static string BoilerLock => GetString(nameof(BoilerLock));
    internal static string Disconnected => GetString(nameof(Disconnected));
    internal static string CompletionActive => GetString(nameof(CompletionActive));
    internal static string PostAskEndActive => GetString(nameof(PostAskEndActive));
    internal static string GenericReset => GetString(nameof(GenericReset));
    internal static string WaitForAuto => GetString(nameof(WaitForAuto));
    internal static string LoginRequired => GetString(nameof(LoginRequired));
    internal static string ScanPrompt => GetString(nameof(ScanPrompt));
    internal static string PhaseBarcodeReceived => GetString(nameof(PhaseBarcodeReceived));
    internal static string PhaseValidatingSteps => GetString(nameof(PhaseValidatingSteps));
    internal static string PhaseValidatingRecipes => GetString(nameof(PhaseValidatingRecipes));
    internal static string PhaseLoadingRecipes => GetString(nameof(PhaseLoadingRecipes));
    internal static string PhaseCreatingDbRecords => GetString(nameof(PhaseCreatingDbRecords));
    internal static string PhaseWaitingForAdapter => GetString(nameof(PhaseWaitingForAdapter));
    internal static string PhaseWaitingForDiagnosticConnection => GetString(nameof(PhaseWaitingForDiagnosticConnection));
    internal static string PlcConnectionLostTitle => GetString(nameof(PlcConnectionLostTitle));

    internal static string PlcConnectionLostToastDetail(double seconds)
    {
        return string.Format(
            CultureInfo.CurrentUICulture,
            GetString("PlcConnectionLostToastDetailFormat"),
            seconds);
    }

    private static string GetString(string name)
    {
        return ResourceManager.GetString(name, CultureInfo.CurrentUICulture)
            ?? throw new InvalidOperationException($"Не найден ресурс сообщения '{name}'.");
    }
}

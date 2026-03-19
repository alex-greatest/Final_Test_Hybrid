using Final_Test_Hybrid.Services.Diagnostic.Models;

namespace Final_Test_Hybrid.Services.Steps.Steps.Coms;

/// <summary>
/// Единая сборка communication-vs-functional сообщений для Coms шагов.
/// </summary>
internal static class ComsStepFailureHelper
{
    public static string BuildReadMessage<T>(
        DiagnosticReadResult<T> result,
        string operation,
        string functionalMessage)
    {
        return BuildMessage(result.FailureKind, operation, result.Error, functionalMessage);
    }

    public static string BuildWriteMessage(
        DiagnosticWriteResult result,
        string operation,
        string functionalMessage)
    {
        return BuildMessage(result.FailureKind, operation, result.Error, functionalMessage);
    }

    private static string BuildMessage(
        DiagnosticFailureKind failureKind,
        string operation,
        string? error,
        string functionalMessage)
    {
        return failureKind == DiagnosticFailureKind.Communication
            ? $"Ошибка связи при {operation}. {error ?? "Неизвестная ошибка"}"
            : functionalMessage;
    }
}

using Final_Test_Hybrid.Services.Diagnostic.Models;
using Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue.Internal;

namespace Final_Test_Hybrid.Services.Diagnostic.Protocol;

/// <summary>
/// Определяет тип ошибки диагностической операции.
/// </summary>
internal static class DiagnosticFailureClassifier
{
    private const string DispatcherNotStartedMessage = "Диспетчер не запущен";
    private const string DispatcherStoppedMessage = "Диспетчер остановлен";
    private const string DispatcherUnavailableMessage = "Диспетчер не инициализирован";
    private const string ReconnectSuppressedMessage = "Команда подавлена во время переподключения";

    public static DiagnosticFailureKind FromException(Exception ex)
    {
        if (CommunicationErrorHelper.IsCommunicationError(ex))
        {
            return DiagnosticFailureKind.Communication;
        }

        return IsDispatcherUnavailable(ex.Message)
            ? DiagnosticFailureKind.Communication
            : DiagnosticFailureKind.Functional;
    }

    private static bool IsDispatcherUnavailable(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return message.Contains(DispatcherNotStartedMessage, StringComparison.OrdinalIgnoreCase)
            || message.Contains(DispatcherStoppedMessage, StringComparison.OrdinalIgnoreCase)
            || message.Contains(DispatcherUnavailableMessage, StringComparison.OrdinalIgnoreCase)
            || message.Contains(ReconnectSuppressedMessage, StringComparison.OrdinalIgnoreCase);
    }
}

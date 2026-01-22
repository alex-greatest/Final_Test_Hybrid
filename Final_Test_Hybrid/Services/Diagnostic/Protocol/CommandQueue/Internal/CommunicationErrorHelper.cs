namespace Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue.Internal;

/// <summary>
/// Вспомогательный класс для определения ошибок связи.
/// </summary>
internal static class CommunicationErrorHelper
{
    /// <summary>
    /// Определяет, является ли исключение ошибкой связи с устройством.
    /// Проходит по всей цепочке InnerException для корректной обработки
    /// исключений NModbus, которые оборачивают реальные ошибки.
    /// </summary>
    /// <param name="ex">Исключение для проверки.</param>
    /// <returns>True если это ошибка связи.</returns>
    public static bool IsCommunicationError(Exception ex)
    {
        var current = ex;
        while (current != null)
        {
            switch (current)
            {
                // Типы ошибок связи для Serial Port
                case TimeoutException or IOException
                    or ObjectDisposedException or UnauthorizedAccessException:
                // InvalidOperationException только если связана с портом
                case InvalidOperationException when
                    (current.Message.Contains("port", StringComparison.OrdinalIgnoreCase) ||
                     current.Message.Contains("closed", StringComparison.OrdinalIgnoreCase) ||
                     current.Message.Contains("open", StringComparison.OrdinalIgnoreCase)):
                    return true;
                default:
                    current = current.InnerException;
                    break;
            }
        }

        return false;
    }
}

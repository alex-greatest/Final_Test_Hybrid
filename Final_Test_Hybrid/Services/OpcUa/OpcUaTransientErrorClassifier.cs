using Opc.Ua;

namespace Final_Test_Hybrid.Services.OpcUa;

/// <summary>
/// Классификатор временных OPC UA ошибок связи.
/// </summary>
internal static class OpcUaTransientErrorClassifier
{
    public static bool IsTransientDisconnect(Exception ex)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            if (current is ServiceResultException serviceResultException
                && IsTransientStatusCode(serviceResultException.StatusCode))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsTransientStatusCode(StatusCode code)
    {
        return code.Code is StatusCodes.BadNotConnected
            or StatusCodes.BadSessionClosed
            or StatusCodes.BadSessionIdInvalid
            or StatusCodes.BadSessionNotActivated
            or StatusCodes.BadConnectionClosed
            or StatusCodes.BadServerNotConnected
            or StatusCodes.BadSecureChannelClosed
            or StatusCodes.BadCommunicationError;
    }
}

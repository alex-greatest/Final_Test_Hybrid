using Opc.Ua;

namespace Final_Test_Hybrid.Services.OpcUa.Subscription;

public static class OpcUaErrorMapper
{
    public static string ToHumanReadable(StatusCode code) => code.Code switch
    {
        StatusCodes.BadNodeIdUnknown => "Тег не найден на сервере",
        StatusCodes.BadNodeIdInvalid => "Неверный формат тега",
        StatusCodes.BadAttributeIdInvalid => "Неверный атрибут",
        StatusCodes.BadNotReadable => "Нет прав на чтение",
        StatusCodes.BadUserAccessDenied => "Доступ запрещён",
        StatusCodes.BadOutOfService => "Сервер недоступен",
        StatusCodes.BadTooManyMonitoredItems => "Превышен лимит подписок",
        StatusCodes.BadMonitoredItemIdInvalid => "Неверный идентификатор элемента",
        StatusCodes.BadSubscriptionIdInvalid => "Неверный идентификатор подписки",
        StatusCodes.BadTimeout => "Превышено время ожидания",
        StatusCodes.BadConnectionClosed => "Соединение закрыто",
        StatusCodes.BadServerNotConnected => "Сервер не подключён",
        _ => $"Ошибка OPC UA: {code}"
    };
}

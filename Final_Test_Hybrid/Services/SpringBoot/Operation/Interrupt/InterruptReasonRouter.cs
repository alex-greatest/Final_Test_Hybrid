using Final_Test_Hybrid.Services.Storage;

namespace Final_Test_Hybrid.Services.SpringBoot.Operation.Interrupt;

/// <summary>
/// Роутер для сохранения причины прерывания.
/// UseMes=true -> MES, UseMes=false -> БД.
/// </summary>
public class InterruptReasonRouter(
    InterruptedOperationService mesService,
    InterruptReasonStorageService dbService)
{
    /// <summary>
    /// Сохраняет причину прерывания.
    /// </summary>
    /// <param name="serialNumber">Серийный номер.</param>
    /// <param name="adminUsername">Имя администратора/оператора.</param>
    /// <param name="reason">Причина прерывания.</param>
    /// <param name="useMes">Режим MES (true) или БД (false). Передаётся явно для консистентности с flow.</param>
    /// <param name="ct">Токен отмены.</param>
    public Task<SaveResult> SaveAsync(
        string serialNumber,
        string adminUsername,
        string reason,
        bool useMes,
        CancellationToken ct)
    {
        return useMes
            ? mesService.SendAsync(serialNumber, adminUsername, reason, ct)
            : dbService.SaveAsync(serialNumber, adminUsername, reason, ct);
    }
}

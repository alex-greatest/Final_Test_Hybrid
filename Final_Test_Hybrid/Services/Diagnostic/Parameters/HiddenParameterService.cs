using Final_Test_Hybrid.Services.Diagnostic.Access;
using Final_Test_Hybrid.Services.Diagnostic.Models;
using Final_Test_Hybrid.Services.Diagnostic.Protocol;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Diagnostic.Parameters;

/// <summary>
/// Сервис для работы со скрытыми параметрами котла (п.4.6 протокола).
/// В режиме стенда: запись в оба Hidden регистра (одинаковые значения).
/// В инженерном режиме: запись в основной регистр (только если не пройден тест).
/// </summary>
public class HiddenParameterService(
    RegisterReader reader,
    RegisterWriter writer,
    AccessLevelManager accessManager,
    ILogger<HiddenParameterService> logger)
{
    /// <summary>
    /// Читает значение скрытого параметра (из основного регистра).
    /// </summary>
    public Task<DiagnosticReadResult<ushort>> ReadAsync(HiddenParameter param, CancellationToken ct = default)
    {
        logger.LogDebug("Чтение скрытого параметра {Name} с адреса {Address}", param.Name, param.ReadAddress);
        return reader.ReadUInt16Async(param.ReadAddress, ct);
    }

    /// <summary>
    /// Записывает значение скрытого параметра.
    /// В режиме Stand: пишет в оба Hidden регистра.
    /// В режиме Engineering: пишет в основной регистр.
    /// В режиме Normal: возвращает ошибку.
    /// </summary>
    public Task<DiagnosticWriteResult> WriteAsync(HiddenParameter param, ushort value, CancellationToken ct = default)
    {
        var level = accessManager.CurrentLevel;
        return level switch
        {
            AccessLevel.Stand => WriteToHiddenAsync(param, value, ct),
            AccessLevel.Engineering => WriteToMainAsync(param, value, ct),
            _ => Task.FromResult(DiagnosticWriteResult.Fail(param.ReadAddress, "Требуется Engineering или Stand режим"))
        };
    }

    private async Task<DiagnosticWriteResult> WriteToHiddenAsync(HiddenParameter param, ushort value, CancellationToken ct)
    {
        logger.LogInformation(
            "Запись скрытого параметра {Name} = {Value} в Hidden регистры ({Hidden1}, {Hidden2})",
            param.Name, value, param.Hidden1Address, param.Hidden2Address);

        var result1 = await writer.WriteUInt16Async(param.Hidden1Address, value, ct).ConfigureAwait(false);
        if (!result1.Success)
        {
            logger.LogError("Ошибка записи в Hidden1 {Address}: {Error}", param.Hidden1Address, result1.Error);
            return result1;
        }

        var result2 = await writer.WriteUInt16Async(param.Hidden2Address, value, ct).ConfigureAwait(false);
        if (result2.Success) return result2;
        logger.LogWarning(
            "Первая попытка записи в Hidden2 {Address} неудачна, повторяю: {Error}",
            param.Hidden2Address, result2.Error);

        result2 = await writer.WriteUInt16Async(param.Hidden2Address, value, ct).ConfigureAwait(false);
        if (result2.Success) return result2;
        logger.LogError("Ошибка записи в Hidden2 {Address} после retry: {Error}", param.Hidden2Address, result2.Error);
        logger.LogWarning(
            "INCONSISTENT STATE: Hidden1 {H1} записан, Hidden2 {H2} — ошибка. Значения могут отличаться!",
            param.Hidden1Address, param.Hidden2Address);

        return result2;
    }

    private async Task<DiagnosticWriteResult> WriteToMainAsync(HiddenParameter param, ushort value, CancellationToken ct)
    {
        logger.LogInformation(
            "Запись скрытого параметра {Name} = {Value} в основной регистр {Address}",
            param.Name, value, param.ReadAddress);

        var result = await writer.WriteUInt16Async(param.ReadAddress, value, ct).ConfigureAwait(false);
        if (!result.Success)
        {
            logger.LogError("Ошибка записи в основной регистр {Address}: {Error}", param.ReadAddress, result.Error);
        }
        return result;
    }
}

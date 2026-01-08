using Final_Test_Hybrid.Services.Diagnostic.Models;
using Final_Test_Hybrid.Services.Diagnostic.Protocol;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Diagnostic.Access;

/// <summary>
/// Управление уровнями доступа ЭБУ котла.
/// </summary>
public class AccessLevelManager(
    RegisterWriter registerWriter,
    ILogger<AccessLevelManager> logger)
{
    // Регистры для записи ключа доступа
    private const ushort AccessKeyAddressHi = 1000;

    // Ключи доступа из протокола
    private const uint EngineeringKey = 0xFA87_CD5E;
    private const uint StandKey = 0xD7F8_DB56;
    private const uint ResetKey = 0x0000_0000;

    /// <summary>
    /// Текущий уровень доступа.
    /// </summary>
    public AccessLevel CurrentLevel { get; private set; } = AccessLevel.Normal;

    /// <summary>
    /// Событие изменения уровня доступа.
    /// </summary>
    public event Action<AccessLevel>? LevelChanged;

    /// <summary>
    /// Устанавливает инженерный режим (ключ 0xFA87_CD5E).
    /// </summary>
    /// <returns>True при успешной установке.</returns>
    public async Task<bool> SetEngineeringModeAsync(CancellationToken ct = default)
    {
        return await SetAccessLevelAsync(AccessLevel.Engineering, EngineeringKey, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Устанавливает режим стенда (ключ 0xD7F8_DB56).
    /// </summary>
    /// <returns>True при успешной установке.</returns>
    public async Task<bool> SetStandModeAsync(CancellationToken ct = default)
    {
        return await SetAccessLevelAsync(AccessLevel.Stand, StandKey, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Сбрасывает в обычный режим (любой ключ != Engineering/Stand).
    /// </summary>
    public async Task ResetToNormalModeAsync(CancellationToken ct = default)
    {
        await SetAccessLevelAsync(AccessLevel.Normal, ResetKey, ct).ConfigureAwait(false);
    }

    private async Task<bool> SetAccessLevelAsync(AccessLevel level, uint key, CancellationToken ct)
    {
        logger.LogInformation("Установка уровня доступа: {Level}", level);
        var result = await registerWriter.WriteUInt32Async(AccessKeyAddressHi, key, ct).ConfigureAwait(false);
        if (!result.Success)
        {
            logger.LogError("Не удалось установить уровень доступа {Level}: {Error}", level, result.Error);
            return false;
        }
        CurrentLevel = level;
        LevelChanged?.Invoke(level);
        logger.LogInformation("Уровень доступа установлен: {Level}", level);
        return true;
    }
}

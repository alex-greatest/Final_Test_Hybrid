using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Final_Test_Hybrid.Services.Diagnostic.Models;
using Final_Test_Hybrid.Services.Diagnostic.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Services.Diagnostic.Access;

/// <summary>
/// Управление уровнями доступа ЭБУ котла.
/// </summary>
public class AccessLevelManager(
    RegisterWriter registerWriter,
    IOptions<DiagnosticSettings> settings,
    ILogger<AccessLevelManager> logger)
{
    private readonly DiagnosticSettings _settings = settings.Value;

    // Адрес ключа доступа из документации (Doc address)
    private const ushort AccessKeyAddressDoc = 1000;

    // Ключи доступа из протокола
    private const uint EngineeringKey = 0xFA87_CD5E;
    private const uint StandKey = 0xD7F8_DB56;
    private const uint ResetKey = 0x0000_0000;

    private readonly object _lock = new();
    private AccessLevel _currentLevel = AccessLevel.Normal;

    /// <summary>
    /// Текущий уровень доступа.
    /// </summary>
    public AccessLevel CurrentLevel
    {
        get { lock (_lock) return _currentLevel; }
        private set { lock (_lock) _currentLevel = value; }
    }

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
    /// Устанавливает режим стенда через Pausable writer (для тестовых шагов).
    /// Поддерживает паузу при Auto OFF.
    /// </summary>
    /// <param name="writer">Pausable writer из TestStepContext.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат записи с детальной ошибкой.</returns>
    public async Task<DiagnosticWriteResult> SetStandModeAsync(
        PausableRegisterWriter writer, CancellationToken ct)
    {
        return await SetAccessLevelAsync(AccessLevel.Stand, StandKey, writer, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Сбрасывает в обычный режим (любой ключ != Engineering/Stand).
    /// </summary>
    public async Task ResetToNormalModeAsync(CancellationToken ct = default)
    {
        await SetAccessLevelAsync(AccessLevel.Normal, ResetKey, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Сбрасывает в обычный режим через Pausable writer (для тестовых шагов).
    /// Поддерживает паузу при Auto OFF.
    /// </summary>
    /// <param name="writer">Pausable writer из TestStepContext.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат записи с детальной ошибкой.</returns>
    public async Task<DiagnosticWriteResult> ResetToNormalModeAsync(
        PausableRegisterWriter writer, CancellationToken ct)
    {
        return await SetAccessLevelAsync(AccessLevel.Normal, ResetKey, writer, ct).ConfigureAwait(false);
    }

    private async Task<bool> SetAccessLevelAsync(AccessLevel level, uint key, CancellationToken ct)
    {
        var result = await SetAccessLevelCoreAsync(level, key, registerWriter, ct).ConfigureAwait(false);
        return result.Success;
    }

    private async Task<DiagnosticWriteResult> SetAccessLevelAsync(
        AccessLevel level, uint key, PausableRegisterWriter writer, CancellationToken ct)
    {
        return await SetAccessLevelCoreAsync(level, key, writer, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Общая логика установки уровня доступа.
    /// </summary>
    private async Task<DiagnosticWriteResult> SetAccessLevelCoreAsync<TWriter>(
        AccessLevel level, uint key, TWriter writer, CancellationToken ct)
        where TWriter : class
    {
        logger.LogInformation("Установка уровня доступа: {Level}", level);

        // Вычисляем Modbus адрес с учётом смещения
        var modbusAddress = (ushort)(AccessKeyAddressDoc - _settings.BaseAddressOffset);
        logger.LogDebug(
            "Запись в регистры {ModbusHi}-{ModbusLo}: ключ 0x{Key:X8} (Doc: {DocHi}-{DocLo})",
            modbusAddress, modbusAddress + 1, key, AccessKeyAddressDoc, AccessKeyAddressDoc + 1);

        var result = writer switch
        {
            RegisterWriter rw => await rw.WriteUInt32Async(modbusAddress, key, ct).ConfigureAwait(false),
            PausableRegisterWriter prw => await prw.WriteUInt32Async(modbusAddress, key, ct).ConfigureAwait(false),
            _ => throw new ArgumentException($"Unsupported writer type: {writer.GetType()}", nameof(writer))
        };

        if (result.Success)
        {
            CurrentLevel = level;
            LevelChanged?.Invoke(level);
            logger.LogInformation("Уровень доступа установлен: {Level}", level);
        }
        else
        {
            logger.LogError("Не удалось установить уровень доступа {Level}: {Error}", level, result.Error);
        }

        return result;
    }
}

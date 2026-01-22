using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Access;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.Diagnostic;

/// <summary>
/// Проверяет корректность записи и чтения регистра.
/// Записывает тестовое значение, читает обратно и сверяет.
/// Использует регистр 1013 (Modbus 1012) — DHW Setpoint (диапазон 35-60°C).
/// </summary>
public class DiagWriteReadVerifyStep(
    DualLogger<DiagWriteReadVerifyStep> logger,
    AccessLevelManager accessLevelManager) : ITestStep
{
    private const ushort TestAddress = 1012;  // Modbus адрес (документ. 1013 - offset 1)
    private const ushort TestValue = 45;      // В пределах диапазона 35-60°C

    public string Id => "diag-write-read-verify";
    public string Name => "DiagWriteReadVerify";
    public string Description => "Верификация записи/чтения Modbus";

    /// <summary>
    /// Выполняет тест записи и верификации чтением.
    /// </summary>
    /// <param name="context">Контекст выполнения шага.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("▶ Старт теста записи/чтения для адреса 0x{Address:X4}", TestAddress);

        // Установить режим Stand для получения прав на запись
        var standResult = await accessLevelManager.SetStandModeAsync(ct);
        if (!standResult)
        {
            return TestStepResult.Fail("Не удалось установить режим Stand");
        }

        var originalValue = await ReadOriginalValueAsync(context, ct);

        if (!originalValue.HasValue)
        {
            return TestStepResult.Fail("Не удалось прочитать исходное значение");
        }

        logger.LogInformation("Исходное значение: 0x{Value:X4}", originalValue.Value);

        var writeResult = await WriteTestValueAsync(context, ct);

        if (!writeResult)
        {
            return TestStepResult.Fail($"Ошибка записи тестового значения 0x{TestValue:X4}");
        }

        var readBackResult = await ReadAndVerifyAsync(context, ct);

        await RestoreOriginalValueAsync(context, originalValue.Value, ct);

        return EvaluateResults(readBackResult.success, readBackResult.value, originalValue.Value);
    }

    /// <summary>
    /// Читает исходное значение регистра.
    /// </summary>
    private async Task<ushort?> ReadOriginalValueAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogDebug("Чтение исходного значения...");
        var result = await context.DiagReader.ReadUInt16Async(TestAddress, ct: ct);

        if (!result.Success)
        {
            logger.LogError("Ошибка чтения исходного значения: {Error}", result.Error);
            return null;
        }

        return result.Value;
    }

    /// <summary>
    /// Записывает тестовое значение.
    /// </summary>
    private async Task<bool> WriteTestValueAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogDebug("Запись тестового значения 0x{Value:X4}...", TestValue);
        var result = await context.DiagWriter.WriteUInt16Async(TestAddress, TestValue, ct: ct);

        if (!result.Success)
        {
            logger.LogError("Ошибка записи: {Error}", result.Error);
            return false;
        }

        logger.LogDebug("Запись успешна");
        return true;
    }

    /// <summary>
    /// Читает записанное значение и проверяет.
    /// </summary>
    private async Task<(bool success, ushort value)> ReadAndVerifyAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogDebug("Чтение записанного значения...");
        var result = await context.DiagReader.ReadUInt16Async(TestAddress, ct: ct);

        if (!result.Success)
        {
            logger.LogError("Ошибка чтения: {Error}", result.Error);
            return (false, 0);
        }

        logger.LogInformation("Прочитано значение: 0x{Value:X4}, ожидалось: 0x{Expected:X4}",
            result.Value, TestValue);

        return (true, result.Value);
    }

    /// <summary>
    /// Восстанавливает исходное значение регистра.
    /// </summary>
    private async Task RestoreOriginalValueAsync(TestStepContext context, ushort originalValue, CancellationToken ct)
    {
        logger.LogDebug("Восстановление исходного значения 0x{Value:X4}...", originalValue);

        var result = await context.DiagWriter.WriteUInt16Async(TestAddress, originalValue, ct: ct);

        if (result.Success)
        {
            logger.LogDebug("Исходное значение восстановлено");
        }
        else
        {
            logger.LogWarning("Не удалось восстановить исходное значение: {Error}", result.Error);
        }
    }

    /// <summary>
    /// Оценивает результаты теста.
    /// </summary>
    private TestStepResult EvaluateResults(bool readSuccess, ushort readValue, ushort originalValue)
    {
        var summary = $"Written=0x{TestValue:X4}, Read=0x{readValue:X4}, Original=0x{originalValue:X4}";

        logger.LogInformation(
            "◼ Результаты верификации:\n" +
            "  Записано: 0x{Written:X4}\n" +
            "  Прочитано: 0x{Read:X4}\n" +
            "  Совпадение: {Match}",
            TestValue, readValue, readValue == TestValue);

        if (!readSuccess)
        {
            return TestStepResult.Fail($"Ошибка чтения после записи. {summary}");
        }

        if (readValue != TestValue)
        {
            return TestStepResult.Fail($"Значения не совпадают: записано 0x{TestValue:X4}, прочитано 0x{readValue:X4}. {summary}");
        }

        return TestStepResult.Pass(summary);
    }
}

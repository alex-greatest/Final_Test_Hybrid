using Final_Test_Hybrid.Models;
using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Common.Settings;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;

namespace Final_Test_Hybrid.Services.Export;

/// <summary>
/// Сервис экспорта истории шагов теста в Excel.
/// </summary>
public class StepHistoryExcelExporter(
    AppSettingsService appSettings,
    BoilerState boilerState,
    DualLogger<StepHistoryExcelExporter> logger)
{
    /// <summary>
    /// Автоэкспорт (fire-and-forget с логированием).
    /// Вызывается из ClearAllExceptScan().
    /// </summary>
    public void ExportIfEnabledAsync(IEnumerable<TestSequenseData> steps)
    {
        if (!appSettings.ExportStepsToExcel)
        {
            return;
        }
        if (string.IsNullOrWhiteSpace(appSettings.ExportPath))
        {
            logger.LogWarning("Путь экспорта не настроен, автоэкспорт пропущен");
            return;
        }
        var stepsCopy = steps.ToList();
        var context = CaptureExportContext();
        _ = Task.Run(() => ExportSafe(stepsCopy, context));
    }

    /// <summary>
    /// Ручной экспорт с возвратом результата.
    /// </summary>
    /// <param name="steps">Коллекция шагов для экспорта.</param>
    /// <returns>Кортеж с результатом операции и сообщением об ошибке.</returns>
    public (bool Success, string? ErrorMessage) Export(IEnumerable<TestSequenseData> steps)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(appSettings.ExportPath))
            {
                return (false, "Путь экспорта не настроен");
            }
            var stepsList = steps.ToList();
            if (stepsList.Count == 0)
            {
                return (false, "Нет данных для экспорта");
            }
            var context = CaptureExportContext();
            ExportCore(stepsList, context);
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка экспорта в Excel");
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Безопасный экспорт с логированием ошибок.
    /// </summary>
    private void ExportSafe(List<TestSequenseData> steps, ExportContext context)
    {
        try
        {
            if (steps.Count == 0)
            {
                return;
            }
            ExportCore(steps, context);
            logger.LogInformation("Автоэкспорт в Excel выполнен успешно");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка автоэкспорта в Excel");
        }
    }

    /// <summary>
    /// Основная логика экспорта.
    /// </summary>
    private void ExportCore(List<TestSequenseData> steps, ExportContext context)
    {
        EnsureDirectoryExists();
        var filePath = BuildFilePath(context);
        SaveWorkbook(filePath, steps);
    }

    /// <summary>
    /// Захватывает контекст экспорта в момент вызова.
    /// </summary>
    private ExportContext CaptureExportContext()
    {
        return new ExportContext(
            boilerState.LastSerialNumber,
            boilerState.LastTestCompletedAt,
            DateTime.Now);
    }

    /// <summary>
    /// Проверяет и создаёт папку для экспорта.
    /// </summary>
    private void EnsureDirectoryExists()
    {
        var path = appSettings.ExportPath;
        if (string.IsNullOrEmpty(path))
        {
            return;
        }
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    /// <summary>
    /// Формирует путь к файлу экспорта.
    /// </summary>
    private string BuildFilePath(ExportContext context)
    {
        var serialNumber = SanitizeFileName(context.SerialNumber ?? "Unknown");
        var testDate = context.TestCompletedAt?.ToString("dd.MM.yyyy HH.mm.ss") ?? "Unknown";
        var exportDate = context.ExportTime.ToString("dd.MM.yyyy HH.mm.ss.fff");
        var fileName = $"{serialNumber}_{testDate}_Выгрузка от {exportDate}.xlsx";
        return Path.Combine(appSettings.ExportPath, fileName);
    }

    /// <summary>
    /// Заменяет недопустимые символы в имени файла.
    /// </summary>
    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
    }

    /// <summary>
    /// Сохраняет данные в Excel файл.
    /// </summary>
    private static void SaveWorkbook(string filePath, List<TestSequenseData> steps)
    {
        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("Шаги теста");
        WriteHeader(worksheet);
        WriteData(worksheet, steps);
        FormatWorksheet(worksheet);
        package.SaveAs(new FileInfo(filePath));
    }

    /// <summary>
    /// Записывает заголовки колонок.
    /// </summary>
    private static void WriteHeader(ExcelWorksheet worksheet)
    {
        worksheet.Cells[1, 1].Value = "Модуль";
        worksheet.Cells[1, 2].Value = "Описание";
        worksheet.Cells[1, 3].Value = "Статус";
        worksheet.Cells[1, 4].Value = "Результаты";
        worksheet.Cells[1, 5].Value = "Пределы";
        FormatHeaderRow(worksheet);
    }

    /// <summary>
    /// Форматирует строку заголовка.
    /// </summary>
    private static void FormatHeaderRow(ExcelWorksheet worksheet)
    {
        var headerRange = worksheet.Cells[1, 1, 1, 5];
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
        headerRange.Style.Fill.BackgroundColor.SetColor(Color.LightGray);
        headerRange.Style.Font.Color.SetColor(Color.Black);
    }

    /// <summary>
    /// Записывает данные шагов.
    /// </summary>
    private static void WriteData(ExcelWorksheet worksheet, List<TestSequenseData> steps)
    {
        for (var i = 0; i < steps.Count; i++)
        {
            var row = i + 2;
            var step = steps[i];
            WriteRow(worksheet, row, step);
            ApplyRowColor(worksheet, row, step.StepStatus);
        }
    }

    /// <summary>
    /// Записывает данные одной строки.
    /// </summary>
    private static void WriteRow(ExcelWorksheet worksheet, int row, TestSequenseData step)
    {
        worksheet.Cells[row, 1].Value = step.Module;
        worksheet.Cells[row, 2].Value = step.Description;
        worksheet.Cells[row, 3].Value = step.Status;
        worksheet.Cells[row, 4].Value = step.Result;
        worksheet.Cells[row, 5].Value = step.Range;
    }

    /// <summary>
    /// Применяет цвет фона строки в зависимости от статуса.
    /// </summary>
    private static void ApplyRowColor(ExcelWorksheet worksheet, int row, TestStepStatus status)
    {
        var rowRange = worksheet.Cells[row, 1, row, 5];
        rowRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
        rowRange.Style.Font.Color.SetColor(Color.Black);
        var backgroundColor = status switch
        {
            TestStepStatus.Error => Color.FromArgb(255, 0, 0),
            TestStepStatus.Success => Color.FromArgb(40, 167, 69),
            TestStepStatus.Running => Color.FromArgb(255, 221, 0),
            _ => Color.White
        };
        rowRange.Style.Fill.BackgroundColor.SetColor(backgroundColor);
    }

    /// <summary>
    /// Применяет форматирование к листу.
    /// </summary>
    private static void FormatWorksheet(ExcelWorksheet worksheet)
    {
        worksheet.Cells.Style.Font.Size = 12;
        worksheet.Cells.AutoFitColumns();
    }

    /// <summary>
    /// Контекст экспорта, захваченный в момент вызова.
    /// </summary>
    private record ExportContext(
        string? SerialNumber,
        DateTime? TestCompletedAt,
        DateTime ExportTime);
}

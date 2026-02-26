using Final_Test_Hybrid.Models.Archive;
using Microsoft.Extensions.Configuration;
using OfficeOpenXml;

namespace Final_Test_Hybrid.Services.Archive;

/// <summary>
/// Сервис экспорта данных архива в Excel файл.
/// </summary>
public class ArchiveExcelExportService(IConfiguration configuration)
{
    private readonly string _exportPath = configuration["Paths:PathToArchiveExport"] ?? "D:/ArchiveExport";

    /// <summary>
    /// Экспортирует данные операции в Excel файл.
    /// </summary>
    /// <param name="data">Данные для экспорта.</param>
    /// <param name="fileName">Имя файла.</param>
    public void Export(ArchiveExportData data, string fileName)
    {
        EnsureDirectoryExists();

        using var package = new ExcelPackage();

        AddResultsSheet(package, "С диапазоном", data, data.NumericWithRange, showRange: true);
        AddResultsSheet(package, "Без диапазона", data, data.SimpleStatus, showRange: false);
        AddResultsSheet(package, "Плата", data, data.BoardParameters, showRange: true);
        AddErrorsSheet(package, "Ошибки", data, data.Errors);
        AddStepTimesSheet(package, "Время шагов", data, data.StepTimes);

        var fullPath = Path.Combine(_exportPath, fileName);
        package.SaveAs(new FileInfo(fullPath));
    }

    /// <summary>
    /// Создает директорию для экспорта, если она не существует.
    /// </summary>
    private void EnsureDirectoryExists()
    {
        if (!Directory.Exists(_exportPath))
        {
            Directory.CreateDirectory(_exportPath);
        }
    }

    /// <summary>
    /// Добавляет заголовок с датой и серийным номером на лист.
    /// </summary>
    private static void AddHeader(ExcelWorksheet ws, ArchiveExportData data)
    {
        var testCompletedAt = data.DateEnd ?? data.DateStart;
        ws.Cells[1, 1].Value = $"Дата и время выгрузки: {data.ExportedAt:dd.MM.yyyy HH:mm:ss}";
        ws.Cells[2, 1].Value = $"Дата и время теста: {FormatUtcToLocal(testCompletedAt)}";
        ws.Cells[3, 1].Value = $"Серийный номер: {data.SerialNumber}";
    }

    /// <summary>
    /// Конвертирует UTC DateTime (с Kind=Unspecified от Npgsql) в строку локального времени.
    /// </summary>
    private static string FormatUtcToLocal(DateTime utcDateTime) =>
        DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc)
            .ToLocalTime()
            .ToString("dd.MM.yyyy HH:mm:ss");

    /// <summary>
    /// Добавляет лист с результатами измерений.
    /// </summary>
    private static void AddResultsSheet(
        ExcelPackage package,
        string name,
        ArchiveExportData data,
        IReadOnlyList<ArchiveResultItem> items,
        bool showRange)
    {
        var ws = package.Workbook.Worksheets.Add(name);
        AddHeader(ws, data);

        var row = 5;
        ws.Cells[row, 1].Value = "Название теста";
        ws.Cells[row, 2].Value = "Параметр";
        ws.Cells[row, 3].Value = "Значение";
        var col = 4;
        if (showRange)
        {
            ws.Cells[row, col++].Value = "Мин";
            ws.Cells[row, col++].Value = "Макс";
        }
        ws.Cells[row, col++].Value = "Статус";
        ws.Cells[row, col].Value = "Ед.изм.";

        foreach (var item in items)
        {
            row++;
            ws.Cells[row, 1].Value = SafeValue(item.TestName);
            ws.Cells[row, 2].Value = SafeValue(item.ParameterName);
            ws.Cells[row, 3].Value = SafeValue(item.Value);
            col = 4;
            if (showRange)
            {
                ws.Cells[row, col++].Value = SafeValue(item.Min);
                ws.Cells[row, col++].Value = SafeValue(item.Max);
            }
            var statusCell = ws.Cells[row, col++];
            switch (item.Status)
            {
                case 1:
                    statusCell.Value = "OK";
                    statusCell.Style.Font.Color.SetColor(Color.Green);
                    break;
                case 2:
                    statusCell.Value = "NOK";
                    statusCell.Style.Font.Color.SetColor(Color.Red);
                    break;
                default:
                    statusCell.Value = "-";
                    break;
            }
            ws.Cells[row, col].Value = SafeValue(item.Unit);
        }

        AutoFitColumnsIfNotEmpty(ws);
    }

    /// <summary>
    /// Добавляет лист с ошибками.
    /// </summary>
    private static void AddErrorsSheet(
        ExcelPackage package,
        string name,
        ArchiveExportData data,
        IReadOnlyList<ArchiveErrorItem> items)
    {
        var ws = package.Workbook.Worksheets.Add(name);
        AddHeader(ws, data);

        var row = 5;
        ws.Cells[row, 1].Value = "Код";
        ws.Cells[row, 2].Value = "Описание";

        foreach (var item in items)
        {
            row++;
            ws.Cells[row, 1].Value = SafeValue(item.Code);
            ws.Cells[row, 2].Value = SafeValue(item.Description);
        }

        AutoFitColumnsIfNotEmpty(ws);
    }

    /// <summary>
    /// Добавляет лист с временем шагов.
    /// </summary>
    private static void AddStepTimesSheet(
        ExcelPackage package,
        string name,
        ArchiveExportData data,
        IReadOnlyList<ArchiveStepTimeItem> items)
    {
        var ws = package.Workbook.Worksheets.Add(name);
        AddHeader(ws, data);

        var row = 5;
        ws.Cells[row, 1].Value = "Шаг";
        ws.Cells[row, 2].Value = "Время";

        foreach (var item in items)
        {
            row++;
            ws.Cells[row, 1].Value = SafeValue(item.StepName);
            ws.Cells[row, 2].Value = SafeValue(item.Duration);
        }

        AutoFitColumnsIfNotEmpty(ws);
    }

    /// <summary>
    /// Автоподгонка ширины колонок (защита от пустого листа).
    /// </summary>
    private static void AutoFitColumnsIfNotEmpty(ExcelWorksheet ws)
    {
        if (ws.Dimension != null)
        {
            ws.Cells[ws.Dimension.Address].AutoFitColumns();
        }
    }

    /// <summary>
    /// Защита от Excel formula injection - экранирует опасные начальные символы.
    /// </summary>
    private static string SafeValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var firstChar = value[0];
        if (firstChar is '=' or '+' or '-' or '@' or '\t' or '\r' or '\n')
        {
            return "'" + value;
        }

        return value;
    }
}

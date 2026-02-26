namespace Final_Test_Hybrid.Models.Archive;

/// <summary>
/// DTO для экспорта данных операции в Excel.
/// </summary>
public record ArchiveExportData
{
    public DateTime DateStart { get; init; }
    public DateTime? DateEnd { get; init; }
    public DateTime ExportedAt { get; init; }
    public string SerialNumber { get; init; } = string.Empty;
    public IReadOnlyList<ArchiveResultItem> NumericWithRange { get; init; } = [];
    public IReadOnlyList<ArchiveResultItem> SimpleStatus { get; init; } = [];
    public IReadOnlyList<ArchiveResultItem> BoardParameters { get; init; } = [];
    public IReadOnlyList<ArchiveErrorItem> Errors { get; init; } = [];
    public IReadOnlyList<ArchiveStepTimeItem> StepTimes { get; init; } = [];
}

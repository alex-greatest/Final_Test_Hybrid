namespace Final_Test_Hybrid.Models.Steps;

public record RawTestMapRow(int RowIndex, string?[] StepNames);

public record RawTestMap(int Index, List<RawTestMapRow> Rows);

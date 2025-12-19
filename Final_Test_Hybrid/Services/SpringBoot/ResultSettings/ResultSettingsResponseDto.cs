using Final_Test_Hybrid.Models.Database;

namespace Final_Test_Hybrid.Services.SpringBoot.ResultSettings;

public class ResultSettingsResponseDto
{
    public string ParameterName { get; set; } = string.Empty;
    public string AddressValue { get; set; } = string.Empty;
    public string? AddressMin { get; set; }
    public string? AddressMax { get; set; }
    public string? AddressStatus { get; set; }
    public PlcType PlcType { get; set; }
    public string? Nominal { get; set; }
    public string? Unit { get; set; }
    public string? Description { get; set; }
    public string AuditType { get; set; } = string.Empty;

    public Models.Database.AuditType ParseAuditType()
    {
        var normalized = AuditType.ToUpperInvariant().Replace("_", "");
        return normalized switch
        {
            "NUMERICWITHRANGE" => Models.Database.AuditType.NumericWithRange,
            "STATUS" => Models.Database.AuditType.Status,
            "SIMPLE" => Models.Database.AuditType.Simple,
            "SIMPLESTATUS" => Models.Database.AuditType.SimpleStatus,
            "BOARDPARAMETERS" => Models.Database.AuditType.BoardParameters,
            _ => Models.Database.AuditType.SimpleStatus
        };
    }
}

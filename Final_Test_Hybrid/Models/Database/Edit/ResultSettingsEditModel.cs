namespace Final_Test_Hybrid.Models.Database.Edit;

public class ResultSettingsEditModel
{
    public long Id { get; init; }
    public long BoilerTypeId { get; set; }
    public string ParameterName { get; set; } = string.Empty;
    public string AddressValue { get; set; } = string.Empty;
    public string? AddressMin { get; set; }
    public string? AddressMax { get; set; }
    public string? AddressStatus { get; set; }
    public PlcType PlcType { get; set; }
    public string? Nominal { get; set; }
    public string? Unit { get; set; }
    public string? Description { get; set; }
    public AuditType AuditType { get; set; }
    public string? BoilerTypeName { get; set; }

    public ResultSettingsEditModel()
    {
    }

    public ResultSettingsEditModel(ResultSettings entity)
    {
        Id = entity.Id;
        BoilerTypeId = entity.BoilerTypeId;
        ParameterName = entity.ParameterName;
        AddressValue = entity.AddressValue;
        AddressMin = entity.AddressMin;
        AddressMax = entity.AddressMax;
        AddressStatus = entity.AddressStatus;
        PlcType = entity.PlcType;
        Nominal = entity.Nominal;
        Unit = entity.Unit;
        Description = entity.Description;
        AuditType = entity.AuditType;
        BoilerTypeName = entity.BoilerType?.Type;
    }

    public ResultSettings ToEntity()
    {
        return new ResultSettings
        {
            Id = Id,
            BoilerTypeId = BoilerTypeId,
            ParameterName = ParameterName,
            AddressValue = AddressValue,
            AddressMin = AddressMin,
            AddressMax = AddressMax,
            AddressStatus = AddressStatus,
            PlcType = PlcType,
            Nominal = Nominal,
            Unit = Unit,
            Description = Description,
            AuditType = AuditType
        };
    }
}

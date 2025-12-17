namespace Final_Test_Hybrid.Models.Database.Edit;

public class ErrorSettingsTemplateEditModel
{
    public long Id { get; init; }
    public long? StepId { get; set; }
    public string? StepName { get; set; }
    public string AddressError { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ErrorSettingsTemplateEditModel()
    {
    }

    public ErrorSettingsTemplateEditModel(ErrorSettingsTemplate entity)
    {
        Id = entity.Id;
        StepId = entity.StepId;
        StepName = entity.Step?.Name;
        AddressError = entity.AddressError;
        Description = entity.Description;
    }

    public ErrorSettingsTemplate ToEntity()
    {
        return new ErrorSettingsTemplate
        {
            Id = Id,
            StepId = StepId,
            AddressError = AddressError,
            Description = Description
        };
    }
}

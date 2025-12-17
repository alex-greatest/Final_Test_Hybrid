namespace Final_Test_Hybrid.Models.Database.Edit;

public class StepFinalTestEditModel
{
    public long Id { get; init; }
    public string Name { get; set; } = string.Empty;

    public StepFinalTestEditModel() { }

    public StepFinalTestEditModel(StepFinalTest entity)
    {
        Id = entity.Id;
        Name = entity.Name;
    }

    public StepFinalTest ToEntity() => new()
    {
        Id = Id,
        Name = Name
    };
}

namespace Final_Test_Hybrid.Models.Database.Edit;

public class RecipeEditModel
{
    public long Id { get; init; }
    public long BoilerTypeId { get; set; }
    public PlcType PlcType { get; set; }
    public bool IsPlc { get; set; }
    public string Address { get; set; } = string.Empty;
    public string TagName { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Unit { get; set; }
    public string? BoilerTypeName { get; set; }

    public RecipeEditModel()
    {
    }

    public RecipeEditModel(Recipe entity)
    {
        Id = entity.Id;
        BoilerTypeId = entity.BoilerTypeId;
        PlcType = entity.PlcType;
        IsPlc = entity.IsPlc;
        Address = entity.Address;
        TagName = entity.TagName;
        Value = entity.Value;
        Description = entity.Description;
        Unit = entity.Unit;
        BoilerTypeName = entity.BoilerType?.Type;
    }

    public Recipe ToEntity()
    {
        return new Recipe
        {
            Id = Id,
            BoilerTypeId = BoilerTypeId,
            PlcType = PlcType,
            IsPlc = IsPlc,
            Address = Address,
            TagName = TagName,
            Value = Value,
            Description = Description,
            Unit = Unit
        };
    }
}

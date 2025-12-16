namespace Final_Test_Hybrid.Models.Database.Edit;

public class BoilerTypeEditModel
{
    public long Id { get; init; }
    public string Article { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;

    public BoilerTypeEditModel()
    {
    }

    public BoilerTypeEditModel(BoilerType entity)
    {
        Id = entity.Id;
        Article = entity.Article;
        Type = entity.Type;
    }

    public BoilerType ToEntity()
    {
        return new BoilerType
        {
            Id = Id,
            Article = Article,
            Type = Type
        };
    }
}

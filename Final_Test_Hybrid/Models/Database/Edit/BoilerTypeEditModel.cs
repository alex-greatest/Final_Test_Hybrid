using Final_Test_Hybrid.Models.Database;

namespace Final_Test_Hybrid.Models.Database.Edit;

public class BoilerTypeEditModel
{
    public long Id { get; init; }
    public string Article { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int Version { get; init; }

    public BoilerTypeEditModel()
    {
    }

    public BoilerTypeEditModel(BoilerType entity)
    {
        Id = entity.Id;
        Article = entity.Article;
        Type = entity.Type;
        Version = entity.Version;
    }

    public BoilerType ToEntity()
    {
        return new BoilerType
        {
            Id = Id,
            Article = Article,
            Type = Type,
            Version = Version
        };
    }
}

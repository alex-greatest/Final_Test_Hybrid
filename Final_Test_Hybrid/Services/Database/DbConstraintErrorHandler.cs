using System.Collections.Frozen;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Final_Test_Hybrid.Services.Database;

public static class DbConstraintErrorHandler
{
    private static readonly FrozenDictionary<string, string> ConstraintMessages = new Dictionary<string, string>
    {
        ["IDX_TB_BOILER_TYPE_UNQ_ARTICLE"] = "Тип котла с таким артикулом уже существует",
        ["IDX_TB_BOILER_TYPE_CYCLE_UNQ_ACTIVE"] = "Активная запись для данного типа котла уже существует",
        ["IDX_TB_RECIPE_UNQ_ADDRESS_BOILER_TYPE"] = "Рецепт с таким адресом для данного типа котла уже существует",
        ["IDX_TB_RECIPE_UNQ_TAG_NAME_BOILER_TYPE"] = "Рецепт с таким именем тега для данного типа котла уже существует"
    }.ToFrozenDictionary();

    private const string DefaultErrorMessage = "Ошибка при работе с базой данных";

    public static string GetUserFriendlyMessage(Exception ex)
    {
        var constraintName = ExtractConstraintName(ex);
        if (constraintName != null && ConstraintMessages.TryGetValue(constraintName, out var message))
        {
            return message;
        }
        return DefaultErrorMessage;
    }

    private static string? ExtractConstraintName(Exception? ex)
    {
        return ex switch
        {
            null => null,
            DbUpdateException dbEx => ExtractConstraintName(dbEx.InnerException),
            PostgresException pgEx => pgEx.ConstraintName,
            _ => ExtractConstraintName(ex.InnerException)
        };
    }
}

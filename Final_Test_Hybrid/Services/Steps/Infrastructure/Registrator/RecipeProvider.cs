using System.Globalization;
using Final_Test_Hybrid.Services.SpringBoot.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

public class RecipeProvider : IRecipeProvider
{
    private readonly Lock _lock = new();
    private Dictionary<string, RecipeResponseDto> _recipesByAddress = new ();
    private IReadOnlyList<RecipeResponseDto> _recipes = [];

    public RecipeResponseDto? GetByAddress(string address)
    {
        lock (_lock)
        {
            return _recipesByAddress.TryGetValue(address, out var recipe) ? recipe : null;
        }
    }

    public IReadOnlyList<RecipeResponseDto> GetAll()
    {
        lock (_lock)
        {
            return _recipes;
        }
    }

    public void SetRecipes(IReadOnlyList<RecipeResponseDto> recipes)
    {
        lock (_lock)
        {
            _recipes = recipes;
            _recipesByAddress = recipes.ToDictionary(r => r.Address);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _recipes = [];
            _recipesByAddress = new Dictionary<string, RecipeResponseDto>();
        }
    }

    public T? GetValue<T>(string address) where T : struct
    {
        var recipe = GetByAddress(address);
        return recipe == null ? null : ConvertValue<T>(recipe.Value);
    }

    public string? GetStringValue(string address)
    {
        var recipe = GetByAddress(address);
        return recipe?.Value;
    }

    private static T? ConvertValue<T>(string value) where T : struct
    {
        var type = typeof(T);
        try
        {
            return type switch
            {
                _ when type == typeof(float) => (T)(object)ParseFloat(value),
                _ when type == typeof(double) => (T)(object)ParseDouble(value),
                _ when type == typeof(int) => (T)(object)int.Parse(value, CultureInfo.InvariantCulture),
                _ when type == typeof(short) => (T)(object)short.Parse(value, CultureInfo.InvariantCulture),
                _ when type == typeof(bool) => (T)(object)ParseBool(value),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    private static float ParseFloat(string value)
    {
        return float.Parse(value.Replace(',', '.'), CultureInfo.InvariantCulture);
    }

    private static double ParseDouble(string value)
    {
        return double.Parse(value.Replace(',', '.'), CultureInfo.InvariantCulture);
    }

    private static bool ParseBool(string value)
    {
        return value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("1", StringComparison.Ordinal);
    }
}

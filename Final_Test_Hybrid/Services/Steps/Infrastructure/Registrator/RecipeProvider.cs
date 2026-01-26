using System.Globalization;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.SpringBoot.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

/// <summary>
/// Предоставляет доступ к рецептам теста по адресам.
/// Потокобезопасный, поддерживает атомарную замену набора рецептов.
/// </summary>
public class RecipeProvider : IRecipeProvider
{
    private readonly Lock _lock = new();
    private readonly DualLogger<RecipeProvider> _logger;
    private Dictionary<string, RecipeResponseDto> _recipesByAddress = new();
    private IReadOnlyList<RecipeResponseDto> _recipes = [];

    /// <summary>
    /// Создаёт провайдер рецептов.
    /// </summary>
    /// <param name="logger">Логгер для файла.</param>
    /// <param name="testStepLogger">Логгер для UI теста.</param>
    public RecipeProvider(
        ILogger<RecipeProvider> logger,
        ITestStepLogger testStepLogger)
    {
        _logger = new DualLogger<RecipeProvider>(logger, testStepLogger);
    }

    /// <inheritdoc />
    public RecipeResponseDto? GetByAddress(string address)
    {
        lock (_lock)
        {
            return _recipesByAddress.TryGetValue(address, out var recipe) ? recipe : null;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<RecipeResponseDto> GetAll()
    {
        lock (_lock)
        {
            return _recipes;
        }
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Если <paramref name="recipes"/> равен null.</exception>
    public void SetRecipes(IReadOnlyList<RecipeResponseDto> recipes)
    {
        ArgumentNullException.ThrowIfNull(recipes);

        var duplicates = new List<string>();
        var dictionary = new Dictionary<string, RecipeResponseDto>();

        foreach (var recipe in recipes)
        {
            if (!dictionary.TryAdd(recipe.Address, recipe))
            {
                duplicates.Add(recipe.Address);
                dictionary[recipe.Address] = recipe;
            }
        }

        if (duplicates.Count > 0)
        {
            _logger.LogWarning("Duplicate recipe addresses (last wins): {Addresses}",
                string.Join(", ", duplicates));
        }

        lock (_lock)
        {
            _recipes = recipes;
            _recipesByAddress = dictionary;
        }

        _logger.LogInformation("Loaded {Count} recipes", recipes.Count);
    }

    /// <inheritdoc />
    public void Clear()
    {
        lock (_lock)
        {
            _recipes = [];
            _recipesByAddress = new Dictionary<string, RecipeResponseDto>();
        }
    }

    /// <inheritdoc />
    public T? GetValue<T>(string address) where T : struct
    {
        var recipe = GetByAddress(address);
        if (recipe?.Value == null)
            return null;

        return ConvertValue<T>(recipe.Value, address);
    }

    /// <inheritdoc />
    public string? GetStringValue(string address)
    {
        var recipe = GetByAddress(address);
        return recipe?.Value;
    }

    /// <summary>
    /// Конвертирует строковое значение в указанный тип.
    /// </summary>
    private T? ConvertValue<T>(string value, string address) where T : struct
    {
        var type = typeof(T);

        if (type == typeof(float))
            return (T?)(object?)TryParseFloat(value, address);
        if (type == typeof(double))
            return (T?)(object?)TryParseDouble(value, address);
        if (type == typeof(int))
            return (T?)(object?)TryParseInt(value, address);
        if (type == typeof(short))
            return (T?)(object?)TryParseShort(value, address);
        if (type == typeof(ushort))
            return (T?)(object?)TryParseUShort(value, address);
        if (type == typeof(bool))
            return (T?)(object?)TryParseBool(value, address);

        _logger.LogWarning("Unsupported type {Type} for address '{Address}'", type.Name, address);
        return null;
    }

    /// <summary>
    /// Парсит строку в float с нормализацией запятой.
    /// </summary>
    private float? TryParseFloat(string value, string address)
    {
        var normalized = value.Replace(',', '.');
        if (float.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            return result;

        _logger.LogWarning("Failed to parse float for address '{Address}': '{Value}'", address, value);
        return null;
    }

    /// <summary>
    /// Парсит строку в double с нормализацией запятой.
    /// </summary>
    private double? TryParseDouble(string value, string address)
    {
        var normalized = value.Replace(',', '.');
        if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            return result;

        _logger.LogWarning("Failed to parse double for address '{Address}': '{Value}'", address, value);
        return null;
    }

    /// <summary>
    /// Парсит строку в int.
    /// </summary>
    private int? TryParseInt(string value, string address)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            return result;

        _logger.LogWarning("Failed to parse int for address '{Address}': '{Value}'", address, value);
        return null;
    }

    /// <summary>
    /// Парсит строку в short.
    /// </summary>
    private short? TryParseShort(string value, string address)
    {
        if (short.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            return result;

        _logger.LogWarning("Failed to parse short for address '{Address}': '{Value}'", address, value);
        return null;
    }

    /// <summary>
    /// Парсит строку в ushort.
    /// </summary>
    private ushort? TryParseUShort(string value, string address)
    {
        if (ushort.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            return result;

        _logger.LogWarning("Failed to parse ushort for address '{Address}': '{Value}'", address, value);
        return null;
    }

    /// <summary>
    /// Парсит строку в bool. Поддерживает true/false и 1/0.
    /// </summary>
    private bool? TryParseBool(string value, string address)
    {
        if (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1")
            return true;
        if (value.Equals("false", StringComparison.OrdinalIgnoreCase) || value == "0")
            return false;

        _logger.LogWarning("Failed to parse bool for address '{Address}': '{Value}'", address, value);
        return null;
    }
}

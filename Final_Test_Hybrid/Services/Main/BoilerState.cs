using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Services.SpringBoot.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;

namespace Final_Test_Hybrid.Services.Main;

public class BoilerState
{
    private readonly Lock _lock = new();
    private readonly IRecipeProvider _recipeProvider;

    public BoilerState(AppSettingsService appSettings, IRecipeProvider recipeProvider)
    {
        _recipeProvider = recipeProvider;
        appSettings.UseMesChanged += _ => Clear();
    }
    private string? _serialNumber;
    private string? _article;
    private bool _isValid;
    private BoilerTypeCycle? _boilerTypeCycle;
    private IReadOnlyList<RecipeResponseDto>? _recipes;
    private string? _lastSerialNumber;

    public event Action? OnChanged;

    public string? SerialNumber
    {
        get
        {
            lock (_lock)
            {
                return _serialNumber;
            }
        }
    }

    public string? Article
    {
        get
        {
            lock (_lock)
            {
                return _article;
            }
        }
    }

    public bool IsValid
    {
        get
        {
            lock (_lock)
            {
                return _isValid;
            }
        }
    }

    public BoilerTypeCycle? BoilerTypeCycle
    {
        get
        {
            lock (_lock)
            {
                return _boilerTypeCycle;
            }
        }
    }

    public IReadOnlyList<RecipeResponseDto>? Recipes
    {
        get
        {
            lock (_lock)
            {
                return _recipes;
            }
        }
    }

    /// <summary>
    /// Серийный номер котла из предыдущего теста.
    /// Сохраняется при вызове Clear() для возможности отслеживания.
    /// </summary>
    public string? LastSerialNumber
    {
        get
        {
            lock (_lock)
            {
                return _lastSerialNumber;
            }
        }
    }

    public void SetData(
        string serialNumber,
        string article,
        bool isValid,
        BoilerTypeCycle? boilerTypeCycle = null,
        IReadOnlyList<RecipeResponseDto>? recipes = null)
    {
        UpdateState(serialNumber, article, isValid, boilerTypeCycle, recipes);
        NotifyChanged();
    }

    public void Clear()
    {
        lock (_lock)
        {
            _lastSerialNumber = _serialNumber;
        }
        UpdateState(serialNumber: null, article: null, isValid: false, boilerTypeCycle: null, recipes: null);
        _recipeProvider.Clear();
        NotifyChanged();
    }

    private void UpdateState(
        string? serialNumber,
        string? article,
        bool isValid,
        BoilerTypeCycle? boilerTypeCycle,
        IReadOnlyList<RecipeResponseDto>? recipes)
    {
        lock (_lock)
        {
            _serialNumber = serialNumber;
            _article = article;
            _isValid = isValid;
            _boilerTypeCycle = boilerTypeCycle;
            _recipes = recipes;
        }
    }

    private void NotifyChanged()
    {
        OnChanged?.Invoke();
    }
}

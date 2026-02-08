using Final_Test_Hybrid.Models;
using Final_Test_Hybrid.Services.SpringBoot.Recipe;
using Microsoft.AspNetCore.Components;

namespace Final_Test_Hybrid.Components.Parameters;

/// <summary>
/// Вкладка отображения рецептов и параметров котла
/// </summary>
public partial class ParametersTab : IDisposable
{
    [Inject]
    private BoilerState BoilerState { get; set; } = null!;

    private IReadOnlyList<RecipeResponseDto> _recipes = [];
    private List<BoilerParam> _boilerParams = [];
    private bool _disposed;

    protected override void OnInitialized()
    {
        BoilerState.OnChanged += HandleStateChanged;
        BoilerState.OnCleared += HandleStateChanged;
        RefreshData();
    }

    private void HandleStateChanged()
    {
        if (_disposed) return;
        _ = InvokeAsync(() =>
        {
            RefreshData();
            StateHasChanged();
        });
    }

    private void RefreshData()
    {
        _recipes = BoilerState.Recipes ?? [];
        _boilerParams =
        [
            new("Артикул", BoilerState.Article ?? ""),
            new("Тип котла", BoilerState.BoilerTypeCycle?.Type ?? "")
        ];
    }

    public void Dispose()
    {
        _disposed = true;
        BoilerState.OnChanged -= HandleStateChanged;
        BoilerState.OnCleared -= HandleStateChanged;
    }

    /// <summary>
    /// Элемент таблицы параметров котла
    /// </summary>
    public record BoilerParam(string Name, string Value);
}

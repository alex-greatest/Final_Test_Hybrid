using Final_Test_Hybrid.Components.Engineer.StandDatabase.Recipe;

namespace Final_Test_Hybrid.Components.Engineer.StandDatabase;

public partial class StandDatabaseDialog
{
    private int _selectedIndex;
    private RecipesGrid? _recipesGrid;
    private bool _needsRefresh;

    private void OnBoilerTypesChanged()
    {
        _needsRefresh = true;
    }

    private async Task OnTabChanged(int index)
    {
        if (index == 1 && _needsRefresh && _recipesGrid != null)
        {
            _needsRefresh = false;
            await _recipesGrid.RefreshAsync();
        }
    }
}

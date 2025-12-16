using Final_Test_Hybrid.Components.Engineer.StandDatabase.Recipe;
using Final_Test_Hybrid.Components.Engineer.StandDatabase.ResultSettings;

namespace Final_Test_Hybrid.Components.Engineer.StandDatabase;

public partial class StandDatabaseDialog
{
    private int _selectedIndex;
    private RecipesGrid? _recipesGrid;
    private ResultSettingsTab? _resultSettingsTab;
    private bool _needsRefresh;

    private void OnBoilerTypesChanged()
    {
        _needsRefresh = true;
    }

    private async Task OnTabChanged(int index)
    {
        if (!_needsRefresh)
        {
            return;
        }
        _needsRefresh = false;
        if (index == 1 && _recipesGrid != null)
        {
            await _recipesGrid.RefreshAsync();
        }
        if (index == 2 && _resultSettingsTab != null)
        {
            await _resultSettingsTab.RefreshAsync();
        }
    }
}

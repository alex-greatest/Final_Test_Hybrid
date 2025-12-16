namespace Final_Test_Hybrid.Components.Engineer.StandDatabase;

public partial class StandDatabaseDialog
{
    private int _selectedIndex;
    private RecipesGrid? _recipesGrid;

    private async Task OnBoilerTypesChanged()
    {
        if (_recipesGrid != null)
        {
            await _recipesGrid.RefreshAsync();
        }
    }
}

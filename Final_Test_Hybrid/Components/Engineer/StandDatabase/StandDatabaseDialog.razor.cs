using Final_Test_Hybrid.Components.Engineer.StandDatabase.Recipe;
using Final_Test_Hybrid.Components.Engineer.StandDatabase.ResultSettings;

namespace Final_Test_Hybrid.Components.Engineer.StandDatabase;

public partial class StandDatabaseDialog
{
    private const int RecipesTabIndex = 1;
    private const int ResultSettingsTabIndex = 2;
    private const int StepsTabIndex = 3;

    private int _selectedIndex;
    private RecipesGrid? _recipesGrid;
    private ResultSettingsTab? _resultSettingsTab;
    private StepFinalTestsGrid? _stepsGrid;
    private readonly HashSet<int> _tabsNeedingRefresh = [];

    private void MarkDependentTabsForRefresh()
    {
        _tabsNeedingRefresh.Add(RecipesTabIndex);
        _tabsNeedingRefresh.Add(ResultSettingsTabIndex);
    }

    private void MarkStepsTabForRefresh()
    {
        _tabsNeedingRefresh.Add(StepsTabIndex);
    }

    private async Task OnTabChanged(int tabIndex)
    {
        var needsRefresh = _tabsNeedingRefresh.Remove(tabIndex);
        if (!needsRefresh)
        {
            return;
        }
        await RefreshTabContent(tabIndex);
    }

    private async Task RefreshTabContent(int tabIndex)
    {
        switch (tabIndex)
        {
            case RecipesTabIndex when _recipesGrid != null:
                await _recipesGrid.RefreshAsync();
                break;
            case ResultSettingsTabIndex when _resultSettingsTab != null:
                await _resultSettingsTab.RefreshAsync();
                break;
            case StepsTabIndex when _stepsGrid != null:
                await _stepsGrid.RefreshAsync();
                break;
        }
    }
}

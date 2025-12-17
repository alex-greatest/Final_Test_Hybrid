using Final_Test_Hybrid.Models.Database.Edit;
using Radzen;

namespace Final_Test_Hybrid.Components.Engineer.StandDatabase.Recipe;

public partial class RecipesGrid
{
    private IList<RecipeEditModel> _selectedRecipes = [];

    private void OnSelectAllChanged(bool value) =>
        _selectedRecipes = value ? _filteredRecipes.ToList() : [];

    private void OnRowSelectChanged(RecipeEditModel item, bool selected) =>
        _selectedRecipes = selected
            ? [.._selectedRecipes.Where(r => r != item), item]
            : _selectedRecipes.Where(r => r != item).ToList();

    private async Task CopySelectedRecipes()
    {
        if (!CanCopy())
        {
            return;
        }
        await TryCopyToTargetAsync();
    }

    private async Task TryCopyToTargetAsync()
    {
        var targetId = await ShowCopyDialogAsync();
        if (!targetId.HasValue)
        {
            return;
        }
        await ExecuteCopyAsync(targetId.Value);
    }

    private bool CanCopy() =>
        _selectedBoilerTypeId.HasValue && _selectedRecipes.Count > 0;

    private async Task<long?> ShowCopyDialogAsync()
    {
        var result = await DialogService.OpenAsync<Modals.CopyRecipesDialog>(
            "Копировать рецепты",
            new Dictionary<string, object>
            {
                { "CurrentBoilerTypeId", _selectedBoilerTypeId!.Value },
                { "BoilerTypes", _boilerTypes }
            },
            new DialogOptions { Width = "500px", CloseDialogOnOverlayClick = false });
        return result as long?;
    }

    private async Task ExecuteCopyAsync(long targetBoilerTypeId)
    {
        var entities = _selectedRecipes.Select(r => r.ToEntity()).ToList();
        var failedRecipes = await RecipeService.CopyRecipesToBoilerTypeAsync(entities, targetBoilerTypeId);
        await LoadDataAsync();
        await ShowCopyResultsAsync(entities.Count - failedRecipes.Count, failedRecipes);
    }

    private async Task ShowCopyResultsAsync(int copiedCount, List<string> failedRecipes)
    {
        ShowSuccessIfAny(copiedCount);
        await ShowFailedDialogIfAny(failedRecipes);
    }

    private void ShowSuccessIfAny(int copiedCount)
    {
        if (copiedCount > 0)
        {
            ShowSuccess($"Скопировано рецептов: {copiedCount}");
        }
    }

    private async Task ShowFailedDialogIfAny(List<string> failedRecipes)
    {
        if (failedRecipes.Count == 0)
        {
            return;
        }
        await DialogService.OpenAsync<Modals.FailedCopiesDialog>(
            "Ошибки копирования",
            new Dictionary<string, object>
            {
                { "FailedItems", failedRecipes },
                { "ErrorMessage", $"Не удалось скопировать рецептов: {failedRecipes.Count}" },
                { "ColumnTitle", "Имя тега" }
            },
            new DialogOptions { Width = "500px", Height = "400px", CloseDialogOnOverlayClick = false });
    }
}

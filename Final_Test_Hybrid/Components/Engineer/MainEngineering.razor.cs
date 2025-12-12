using Microsoft.AspNetCore.Components;
using Radzen;

namespace Final_Test_Hybrid.Components.Engineer;

public partial class MainEngineering
{
    [Inject]
    public required DialogService DialogService { get; set; }

    private async Task OpenTestSequenceEditor()
    {
        await DialogService.OpenAsync<Sequence.TestSequenceEditor>("Редактор шагов теста",
               new Dictionary<string, object>(),
               new DialogOptions
               { 
                   Width = "95vw", 
                   Height = "95vh", 
                   Resizable = true, 
                   Draggable = true,
                   CssClass = "test-sequence-editor-dialog",
                   CloseDialogOnOverlayClick = false
               });
    }
}
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Final_Test_Hybrid.Components.Engineer;

public partial class MainEngineering
{
    [Inject]
    public required DialogService DialogService { get; set; }
    private string selectedLanguage = "French (fr)";
    private readonly List<string> languages =
    [
        "English (en)",
        "French (fr)",
        "German (de)",
        "Spanish (es)",
        "Italian (it)",
        "Russian (ru)"
    ];

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
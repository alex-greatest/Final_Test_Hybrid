using Final_Test_Hybrid.Services.Common.Settings;
using Microsoft.AspNetCore.Components;

namespace Final_Test_Hybrid.Components.Engineer;

public partial class SwitchMes
{
    [Inject]
    public required AppSettingsService AppSettingsService { get; set; }
    private bool _useMes;

    protected override void OnInitialized()
    {
        _useMes = AppSettingsService.UseMes;
    }

    private void OnMesSettingChanged(bool value)
    {
        AppSettingsService.SaveUseMes(value);
    }
}

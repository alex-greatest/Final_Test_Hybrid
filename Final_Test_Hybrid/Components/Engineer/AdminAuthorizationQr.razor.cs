using Final_Test_Hybrid.Components.Engineer.Modals;
using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Final_Test_Hybrid.Components.Engineer;

public partial class AdminAuthorizationQr : IDisposable
{
    [Inject]
    public required AppSettingsService AppSettingsService { get; set; }
    [Inject]
    public required DialogService DialogService { get; set; }
    [Inject]
    public required TestSequenseService TestSequenseService { get; set; }
    [Inject]
    public required AutoReadySubscription AutoReadySubscription { get; set; }
    private bool _useAdminQrAuth;

    private bool IsOnScanStep
    {
        get
        {
            var currentStep = TestSequenseService.Data.FirstOrDefault();
            return currentStep?.Module is "Сканирование штрихкода" or "Сканирование штрихкода MES";
        }
    }
    private bool IsWaitingForAuto => !TestSequenseService.Data.Any() && !AutoReadySubscription.IsReady;
    private bool CanInteract => IsOnScanStep || IsWaitingForAuto || !TestSequenseService.Data.Any();

    protected override void OnInitialized()
    {
        _useAdminQrAuth = AppSettingsService.UseAdminQrAuth;
        TestSequenseService.OnDataChanged += HandleStateChanged;
        AutoReadySubscription.OnChange += HandleStateChanged;
    }

    private void HandleStateChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    private async Task OnCheckboxClick()
    {
        if (!CanInteract)
        {
            return;
        }
        var result = await ShowPasswordDialog();
        if (!result)
        {
            return;
        }
        _useAdminQrAuth = !_useAdminQrAuth;
        AppSettingsService.SaveUseAdminQrAuth(_useAdminQrAuth);
    }

    private async Task<bool> ShowPasswordDialog()
    {
        var result = await DialogService.OpenAsync<PasswordDialog>("Введите пароль",
            new Dictionary<string, object>(),
            new DialogOptions { Width = "350px", CloseDialogOnOverlayClick = false });
        return result is true;
    }

    public void Dispose()
    {
        TestSequenseService.OnDataChanged -= HandleStateChanged;
        AutoReadySubscription.OnChange -= HandleStateChanged;
    }
}

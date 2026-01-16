using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Common.UI;

public class BlazorUiDispatcher(
    BlazorDispatcherAccessor accessor,
    ILogger<BlazorUiDispatcher> logger) : IUiDispatcher
{
    public void Dispatch(Action action)
    {
        _ = DispatchWithErrorHandling(action);
    }

    private async Task DispatchWithErrorHandling(Action action)
    {
        try
        {
            await accessor.InvokeAsync(action);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error dispatching UI action");
        }
    }
}

namespace Final_Test_Hybrid.Services.Common.UI;

public class BlazorUiDispatcher(BlazorDispatcherAccessor accessor) : IUiDispatcher
{
    public void Dispatch(Action action)
    {
        _ = accessor.InvokeAsync(action);
    }
}

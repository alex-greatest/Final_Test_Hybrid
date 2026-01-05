namespace Final_Test_Hybrid.Services.Common.UI;

public class BlazorDispatcherAccessor
{
    private Func<Action, Task>? _invokeAsync;
    private Action? _stateHasChanged;

    public void Initialize(Func<Action, Task> invokeAsync, Action stateHasChanged)
    {
        _invokeAsync = invokeAsync;
        _stateHasChanged = stateHasChanged;
    }

    public Task InvokeAsync(Action action)
    {
        if (_invokeAsync == null)
        {
            action();
            return Task.CompletedTask;
        }
        return _invokeAsync(() =>
        {
            action();
            _stateHasChanged?.Invoke();
        });
    }
}

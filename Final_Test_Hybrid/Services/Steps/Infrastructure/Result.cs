namespace Final_Test_Hybrid.Services.Steps.Infrastructure;

public readonly struct Result<T>
{
    public T? Value { get; }
    public StepResult? Error { get; }
    public bool IsSuccess => Error == null;

    private Result(T? value, StepResult? error) => (Value, Error) = (value, error);

    public static Result<T> Ok(T value) => new(value, null);
    public static Result<T> Fail(StepResult error) => new(default, error);
}

public static class ResultExtensions
{
    public static async Task<Result<TOut>> ThenAsync<TIn, TOut>(
        this Result<TIn> result,
        Func<TIn, Task<Result<TOut>>> next)
    {
        if (!result.IsSuccess) return Result<TOut>.Fail(result.Error!);
        return await next(result.Value!);
    }

    public static async Task<Result<TOut>> ThenAsync<TIn, TOut>(
        this Task<Result<TIn>> resultTask,
        Func<TIn, Task<Result<TOut>>> next)
    {
        var result = await resultTask;
        if (!result.IsSuccess) return Result<TOut>.Fail(result.Error!);
        return await next(result.Value!);
    }

    public static async Task<StepResult> Map<T>(
        this Task<Result<T>> resultTask,
        Func<T, StepResult> map)
    {
        var result = await resultTask;
        if (!result.IsSuccess) return result.Error!;
        return map(result.Value!);
    }
}

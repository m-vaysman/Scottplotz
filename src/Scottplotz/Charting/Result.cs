namespace Scottplotz.Charting;

public abstract record Result<T>
{
    public sealed record Ok(T Value) : Result<T>;

    public sealed record Fail(ChartProblem Problem) : Result<T>;

    public static Result<T> Success(T value) => new Ok(value);

    public static Result<T> Error(ChartProblem problem) => new Fail(problem);
}

public static class Result
{
    public static Result<TOut> Bind<T, TOut>(this Result<T> result, Func<T, Result<TOut>> next) =>
        result switch
        {
            Result<T>.Ok ok => next(ok.Value),
            Result<T>.Fail fail => Result<TOut>.Error(fail.Problem),
            _ => throw new InvalidOperationException("Unknown result type.")
        };
}

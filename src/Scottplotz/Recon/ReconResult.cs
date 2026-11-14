using Scottplotz.Charting;

namespace Scottplotz.Recon;

public static class ReconResult
{
    public static Result<T> Ok<T>(T value) => Result<T>.Success(value);

    public static Result<TOut> Select<T, TOut>(this Result<T> result, Func<T, TOut> map) =>
        result.Bind(value => Result<TOut>.Success(map(value)));

    public static Result<TOut> SelectMany<T, TMid, TOut>(
        this Result<T> result,
        Func<T, Result<TMid>> next,
        Func<T, TMid, TOut> map) =>
        result.Bind(value => next(value).Bind(mid => Result<TOut>.Success(map(value, mid))));

    public static Result<IReadOnlyList<T>> Collect<T>(this IEnumerable<Result<T>> results)
    {
        var values = new List<T>();
        foreach (var result in results)
        {
            if (result is Result<T>.Fail fail)
                return Result<IReadOnlyList<T>>.Error(fail.Problem);

            values.Add(((Result<T>.Ok)result).Value);
        }

        return Result<IReadOnlyList<T>>.Success(values);
    }
}

namespace Scottplotz.Charting;

public enum InvalidNumberPolicy
{
    Reject,
    Drop
}

public sealed record ParseLimits(int MaxPoints, InvalidNumberPolicy InvalidNumbers)
{
    public static ParseLimits Default { get; } = new(100_000, InvalidNumberPolicy.Reject);
}

namespace Scottplotz.Rules;

public static class SeriesStats
{
    public static double CorrelationWithIndex(IReadOnlyList<double> values)
    {
        var n = values.Count;
        if (n < 2)
            return 1;

        double sumX = (n - 1L) * n / 2.0;
        double sumY = values.Sum();
        double sumXx = (n - 1L) * n * (2L * n - 1) / 6.0;
        double sumYy = values.Sum(value => value * value);
        double sumXy = values.Select((value, index) => value * index).Sum();
        var numerator = n * sumXy - sumX * sumY;
        var denominator = Math.Sqrt((n * sumXx - sumX * sumX) * (n * sumYy - sumY * sumY));
        return denominator == 0 ? 0 : numerator / denominator;
    }

    public static bool IsStrictlyMonotonic(IReadOnlyList<double> values) =>
        values.Count < 2
        || values.Zip(values.Skip(1), (left, right) => right > left).All(increasing => increasing)
        || values.Zip(values.Skip(1), (left, right) => right < left).All(decreasing => decreasing);
}

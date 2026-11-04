namespace Scottplotz.Rendering;

public static class CategoryTicks
{
    public const int MinimumLabelGap = 36;

    public static int Step(int labelCount, int plotWidth)
    {
        if (labelCount <= 1)
            return 1;

        var usable = Math.Max(MinimumLabelGap, plotWidth - 120);
        var fit = Math.Max(1, usable / MinimumLabelGap);
        return Math.Max(1, (int)Math.Ceiling(labelCount / (double)fit));
    }

    public static (double[] Positions, string[] Labels) Select(IReadOnlyList<string> labels, int plotWidth)
    {
        var step = Step(labels.Count, plotWidth);
        var chosen = labels
            .Select((label, index) => (label, index))
            .Where(item => item.index % step == 0)
            .ToArray();

        return (
            chosen.Select(item => (double)item.index).ToArray(),
            chosen.Select(item => item.label).ToArray());
    }
}

namespace Scottplotz.Rendering;

public static class DateTicks
{
    public static bool WholeDays(IReadOnlyList<DateTime> dates) =>
        dates.Count > 0 && dates.All(date => date.TimeOfDay == TimeSpan.Zero);
}

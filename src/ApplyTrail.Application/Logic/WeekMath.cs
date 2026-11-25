namespace ApplyTrail.Application.Logic;

public static class WeekMath
{
    /// <summary>Weeks start on Monday, which is how people in Ireland plan a job-search week.</summary>
    public static DateOnly StartOfWeek(DateOnly date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7; // Monday = 0 ... Sunday = 6
        return date.AddDays(-offset);
    }
}

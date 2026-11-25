using ApplyTrail.Application.Logic;

namespace ApplyTrail.UnitTests;

public class WeekMathTests
{
    [Theory]
    [InlineData(2026, 11, 9, 2026, 11, 9)]   // Monday stays put
    [InlineData(2026, 11, 13, 2026, 11, 9)]  // Friday
    [InlineData(2026, 11, 15, 2026, 11, 9)]  // Sunday belongs to the week that is ending
    [InlineData(2026, 11, 16, 2026, 11, 16)] // next Monday
    [InlineData(2026, 1, 1, 2025, 12, 29)]   // crosses a year boundary
    public void Weeks_start_on_monday(int y, int m, int d, int ey, int em, int ed)
    {
        Assert.Equal(new DateOnly(ey, em, ed), WeekMath.StartOfWeek(new DateOnly(y, m, d)));
    }
}

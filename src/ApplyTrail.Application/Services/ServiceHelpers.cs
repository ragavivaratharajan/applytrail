using ApplyTrail.Application.Abstractions;
using ApplyTrail.Domain;
using Microsoft.EntityFrameworkCore;

namespace ApplyTrail.Application.Services;

internal static class ServiceHelpers
{
    /// <summary>How far a client-supplied "today" may drift from the server clock (time zones plus a skewed PC clock).</summary>
    private const int MaxDateSkewDays = 2;

    /// <summary>The user's calendar date: what the client says, if it is plausible, otherwise the UTC date.</summary>
    public static DateOnly ResolveToday(DateOnly? supplied, DateTime nowUtc)
    {
        var server = DateOnly.FromDateTime(nowUtc);
        if (supplied is not { } value)
        {
            return server;
        }

        if (Math.Abs(value.DayNumber - server.DayNumber) > MaxDateSkewDays)
        {
            throw new BadRequestException(
                "Your device's date does not match the server's.",
                "Check the date and time on your computer and try again.");
        }

        return value;
    }

    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static async Task<UserSettings> GetSettingsAsync(
        this IApplicationDbContext db, Guid userId, CancellationToken ct)
    {
        var row = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.WeeklyGoal, u.FollowUpAfterDays, u.StaleAfterDays })
            .FirstOrDefaultAsync(ct)
            ?? throw new UnauthorizedException();

        return new UserSettings(row.WeeklyGoal, row.FollowUpAfterDays, row.StaleAfterDays);
    }
}

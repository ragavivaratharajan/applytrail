using System.ComponentModel.DataAnnotations;
using ApplyTrail.Domain;

namespace ApplyTrail.Application.Dtos;

public record GoalProgressDto(int Target, int AppliedThisWeek);

public record StaleApplicationDto(
    Guid Id, string Company, string RoleTitle, ApplicationStatus Status, int DaysSinceActivity);

public record DashboardSummaryDto(
    GoalProgressDto Goal,
    IReadOnlyList<ReminderDto> Overdue,
    IReadOnlyList<ReminderDto> Today,
    IReadOnlyList<ReminderDto> Upcoming,
    IReadOnlyList<StaleApplicationDto> Stale,
    IReadOnlyDictionary<string, int> StatusCounts);

public record WeeklyPointDto(DateOnly WeekStart, int Applied, int Responded);

public record FunnelDto(int Applied, int Responded, int Interviewed, int Offers);

public record SourceStatDto(JobSource Source, int Applied, int Responded, int Interviewed);

/// <summary>Rates are fractions between 0 and 1.</summary>
public record InsightsDto(
    FunnelDto Funnel,
    double ResponseRate,
    double InterviewRate,
    double? AverageDaysToResponse,
    IReadOnlyList<WeeklyPointDto> Weekly,
    IReadOnlyList<SourceStatDto> BySource,
    int StaleCount);

public record UserSettingsDto(string Email, int WeeklyGoal, int FollowUpAfterDays, int StaleAfterDays);

public class UpdateSettingsRequest
{
    [Range(1, 200)]
    public int WeeklyGoal { get; set; } = 10;

    [Range(1, 60)]
    public int FollowUpAfterDays { get; set; } = 7;

    [Range(1, 120)]
    public int StaleAfterDays { get; set; } = 10;
}

using ApplyTrail.Application.Dtos;
using ApplyTrail.Application.Logic;
using ApplyTrail.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace ApplyTrail.Api.Controllers;

[ApiController]
[Route("api")]
public class DashboardController(DashboardService dashboard) : ControllerBase
{
    /// <summary>What to do today: overdue and upcoming reminders, applications going quiet, weekly goal.</summary>
    [HttpGet("dashboard")]
    public Task<DashboardSummaryDto> Summary([FromQuery] DateOnly? today, CancellationToken ct) =>
        dashboard.GetSummaryAsync(today, ct);

    /// <summary>Response rate, interview rate, time to first reply, weekly volume and per-source results.</summary>
    [HttpGet("insights")]
    public Task<InsightsDto> Insights(
        [FromQuery] int weeks = InsightsCalculator.DefaultWeeks, [FromQuery] DateOnly? today = null, CancellationToken ct = default) =>
        dashboard.GetInsightsAsync(weeks, today, ct);
}

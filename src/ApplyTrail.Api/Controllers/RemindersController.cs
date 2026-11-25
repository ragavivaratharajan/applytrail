using ApplyTrail.Application.Dtos;
using ApplyTrail.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace ApplyTrail.Api.Controllers;

[ApiController]
[Route("api")]
public class RemindersController(ReminderService reminders) : ControllerBase
{
    /// <summary>Open reminders across all live applications. <c>today</c> is the caller's calendar date.</summary>
    [HttpGet("reminders")]
    public Task<IReadOnlyList<ReminderDto>> List(
        [FromQuery] ReminderBucket bucket = ReminderBucket.All, [FromQuery] DateOnly? today = null, CancellationToken ct = default) =>
        reminders.ListAsync(bucket, today, ct);

    [HttpPost("applications/{applicationId:guid}/reminders")]
    [ProducesResponseType<ReminderDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(Guid applicationId, CreateReminderRequest request, CancellationToken ct)
    {
        var created = await reminders.CreateAsync(applicationId, request, ct);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPost("applications/{applicationId:guid}/reminders/{reminderId:guid}/complete")]
    public Task<ReminderDto> Complete(
        Guid applicationId, Guid reminderId, CompleteReminderRequest request, CancellationToken ct) =>
        reminders.CompleteAsync(applicationId, reminderId, request, ct);

    [HttpPost("applications/{applicationId:guid}/reminders/{reminderId:guid}/snooze")]
    public Task<ReminderDto> Snooze(
        Guid applicationId, Guid reminderId, SnoozeReminderRequest request, CancellationToken ct) =>
        reminders.SnoozeAsync(applicationId, reminderId, request, ct);
}

using ApplyTrail.Application.Dtos;
using ApplyTrail.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace ApplyTrail.Api.Controllers;

[ApiController]
[Route("api/applications")]
public class ApplicationsController(ApplicationService applications) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<ApplicationDto>> List([FromQuery] ApplicationQuery query, CancellationToken ct) =>
        applications.ListAsync(query, ct);

    [HttpGet("{id:guid}")]
    public Task<ApplicationDetailDto> Get(Guid id, CancellationToken ct) => applications.GetAsync(id, ct);

    [HttpPost]
    [ProducesResponseType<ApplicationDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateApplicationRequest request, CancellationToken ct)
    {
        var created = await applications.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public Task<ApplicationDto> Update(Guid id, UpdateApplicationRequest request, CancellationToken ct) =>
        applications.UpdateAsync(id, request, ct);

    [HttpPost("{id:guid}/status")]
    public Task<ApplicationDto> ChangeStatus(Guid id, ChangeStatusRequest request, CancellationToken ct) =>
        applications.ChangeStatusAsync(id, request, ct);

    [HttpPost("{id:guid}/archive")]
    public Task<ApplicationDto> Archive(Guid id, CancellationToken ct) => applications.ArchiveAsync(id, true, ct);

    [HttpPost("{id:guid}/unarchive")]
    public Task<ApplicationDto> Unarchive(Guid id, CancellationToken ct) => applications.ArchiveAsync(id, false, ct);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await applications.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/events")]
    [ProducesResponseType<ActivityEventDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> AddEvent(Guid id, CreateEventRequest request, CancellationToken ct)
    {
        var created = await applications.AddEventAsync(id, request, ct);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    /// <summary>"Have I already applied to this?" Call while the form is being filled in.</summary>
    [HttpPost("check-duplicates")]
    public Task<IReadOnlyList<DuplicateMatchDto>> CheckDuplicates(DuplicateCheckRequest request, CancellationToken ct) =>
        applications.CheckDuplicatesAsync(request, ct);
}

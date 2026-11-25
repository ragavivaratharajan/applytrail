using ApplyTrail.Application.Dtos;
using ApplyTrail.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace ApplyTrail.Api.Controllers;

[ApiController]
[Route("api/me")]
public class MeController(SettingsService settings) : ControllerBase
{
    [HttpGet]
    public Task<UserSettingsDto> Get(CancellationToken ct) => settings.GetAsync(ct);

    [HttpPut("settings")]
    public Task<UserSettingsDto> Update(UpdateSettingsRequest request, CancellationToken ct) =>
        settings.UpdateAsync(request, ct);

    /// <summary>Deletes the account and all its data. Requires the password again.</summary>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(DeleteAccountRequest request, CancellationToken ct)
    {
        await settings.DeleteAccountAsync(request, ct);
        Response.Cookies.Delete(AuthController.RefreshCookieName, new CookieOptions { Path = "/api/auth" });
        return NoContent();
    }
}

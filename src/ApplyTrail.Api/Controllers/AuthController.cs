using ApplyTrail.Application.Abstractions;
using ApplyTrail.Application.Dtos;
using ApplyTrail.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ApplyTrail.Api.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
[EnableRateLimiting("auth")]
public class AuthController(AuthService auth, AuthSettings settings) : ControllerBase
{
    public const string RefreshCookieName = "applytrail_refresh";
    private const string CookiePath = "/api/auth";

    [HttpPost("register")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        var result = await auth.RegisterAsync(request, ct);
        SetRefreshCookie(result);
        return StatusCode(StatusCodes.Status201Created, result.Response);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await auth.LoginAsync(request, ct);
        SetRefreshCookie(result);
        return result.Response;
    }

    /// <summary>Swaps the HttpOnly refresh cookie for a new access token (and a rotated cookie).</summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken ct)
    {
        try
        {
            var result = await auth.RefreshAsync(Request.Cookies[RefreshCookieName], ct);
            SetRefreshCookie(result);
            return result.Response;
        }
        catch (UnauthorizedException ex)
        {
            // Answered here, not rethrown: the exception middleware clears response headers, which would drop this.
            ClearRefreshCookie(); // a dead cookie should not be sent again
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: ex.Title);
        }
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await auth.LogoutAsync(Request.Cookies[RefreshCookieName], ct);
        ClearRefreshCookie();
        return NoContent();
    }

    private void SetRefreshCookie(AuthResult result)
    {
        // A refresh answered inside the reuse-grace window carries no new token: the browser already has the winner's.
        if (result.RefreshToken is null || result.RefreshExpiresAt is not { } expiresAt)
        {
            return;
        }

        Response.Cookies.Append(RefreshCookieName, result.RefreshToken, CookieOptionsFor(
            new DateTimeOffset(DateTime.SpecifyKind(expiresAt, DateTimeKind.Utc))));
    }

    private void ClearRefreshCookie() => Response.Cookies.Delete(RefreshCookieName, CookieOptionsFor(null));

    private CookieOptions CookieOptionsFor(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,                 // JavaScript can never read it, so XSS cannot steal the session
        Secure = settings.CookieSecure,
        SameSite = SameSiteMode.Strict,  // never sent on cross-site requests, which blocks CSRF on /refresh
        Path = CookiePath,               // only travels to the auth endpoints
        IsEssential = true,
        Expires = expires,
    };
}

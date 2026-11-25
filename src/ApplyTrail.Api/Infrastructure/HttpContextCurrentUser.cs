using ApplyTrail.Application.Abstractions;

namespace ApplyTrail.Api.Infrastructure;

/// <summary>Reads the signed-in user's id from the "sub" claim of the validated access token.</summary>
public sealed class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid Id
    {
        get
        {
            var subject = accessor.HttpContext?.User.FindFirst("sub")?.Value;
            return Guid.TryParse(subject, out var id) ? id : throw new UnauthorizedException();
        }
    }
}

using ApplyTrail.Domain;

namespace ApplyTrail.Application.Abstractions;

public interface ICurrentUser
{
    /// <summary>The signed-in user's id. Throws <see cref="UnauthorizedException"/> if there is none.</summary>
    Guid Id { get; }
}

public interface IPasswordService
{
    string Hash(string password);

    /// <summary>
    /// Checks a password. Pass a null hash when the user does not exist so the
    /// time taken does not reveal which emails are registered.
    /// </summary>
    bool Verify(string? hash, string password);
}

public sealed record AccessToken(string Value, DateTime ExpiresAt);

public sealed record NewRefreshToken(string Raw, string Hash, DateTime ExpiresAt);

public interface ITokenService
{
    AccessToken CreateAccessToken(User user, DateTime nowUtc);
    NewRefreshToken CreateRefreshToken(DateTime nowUtc);
    string HashRefreshToken(string rawToken);
}

/// <summary>Server-side switches for authentication behaviour.</summary>
public sealed record AuthSettings(bool AllowRegistration, bool CookieSecure);

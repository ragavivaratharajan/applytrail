using ApplyTrail.Application.Abstractions;
using ApplyTrail.Application.Dtos;
using ApplyTrail.Domain;
using Microsoft.EntityFrameworkCore;

namespace ApplyTrail.Application.Services;

/// <summary>
/// Sign-up, sign-in and session renewal. Access tokens are short-lived and live in memory in the browser;
/// the refresh token is a random value held in an HttpOnly cookie and stored here only as a SHA-256 hash.
/// Every refresh rotates the token, and presenting an already-rotated token again is treated as theft.
/// </summary>
public sealed class AuthService(
    IApplicationDbContext db,
    IPasswordService passwords,
    ITokenService tokens,
    TimeProvider clock,
    AuthSettings settings)
{
    /// <summary>
    /// Two browser tabs can fire a refresh at the same instant with the same cookie. A token that was
    /// rotated within this window gets a fresh access token but no new refresh token (the browser already
    /// holds the winner's cookie). After the window, reuse revokes every session for that user.
    /// </summary>
    public static readonly TimeSpan ReuseGrace = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan ExpiredTokenRetention = TimeSpan.FromDays(30);

    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        if (!settings.AllowRegistration)
        {
            throw new ForbiddenException("Registration is disabled on this server.");
        }

        var email = NormalizeEmail(request.Email);
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
        {
            throw new ConflictException("An account with this email already exists.");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var user = User.Create(email, passwords.Hash(request.Password), now);
        db.Users.Add(user);
        var result = IssueTokens(user, now);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Two sign-ups with the same email racing past the check above: the unique index decides.
            throw new ConflictException("An account with this email already exists.");
        }

        return result;
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = NormalizeEmail(request.Email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        // Always run the password check, even for unknown emails, so timing does not reveal who is registered.
        var passwordOk = passwords.Verify(user?.PasswordHash, request.Password);
        if (!passwordOk || user is null)
        {
            throw new UnauthorizedException("Invalid email or password.");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var result = IssueTokens(user, now);
        await RemoveLongExpiredTokensAsync(user.Id, now, ct);
        await db.SaveChangesAsync(ct);
        return result;
    }

    public async Task<AuthResult> RefreshAsync(string? rawToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            throw new UnauthorizedException("Session expired. Please sign in again.");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var hash = tokens.HashRefreshToken(rawToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct)
            ?? throw new UnauthorizedException("Session expired. Please sign in again.");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == stored.UserId, ct)
            ?? throw new UnauthorizedException("Session expired. Please sign in again.");

        if (stored.IsActive(now))
        {
            var next = tokens.CreateRefreshToken(now);
            stored.Revoke(now, replacedByTokenHash: next.Hash);
            db.RefreshTokens.Add(RefreshToken.Create(user.Id, next.Hash, next.ExpiresAt, now));
            await RemoveLongExpiredTokensAsync(user.Id, now, ct);
            await db.SaveChangesAsync(ct);

            var access = tokens.CreateAccessToken(user, now);
            return new AuthResult(new AuthResponse(access.Value, access.ExpiresAt, user.Email), next.Raw, next.ExpiresAt);
        }

        var rotatedRecently = stored.RevokedAt is { } revokedAt
            && stored.ReplacedByTokenHash is not null
            && now - revokedAt <= ReuseGrace;

        if (rotatedRecently)
        {
            var access = tokens.CreateAccessToken(user, now);
            return new AuthResult(new AuthResponse(access.Value, access.ExpiresAt, user.Email), null, null);
        }

        if (stored.ReplacedByTokenHash is not null)
        {
            // A rotated-out token came back after the grace window: someone has a copy. End every session.
            await RevokeAllAsync(user.Id, now, ct);
            await db.SaveChangesAsync(ct);
        }

        throw new UnauthorizedException("Session expired. Please sign in again.");
    }

    /// <summary>Ends the session behind this refresh token. Never throws: logging out twice is fine.</summary>
    public async Task LogoutAsync(string? rawToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var hash = tokens.HashRefreshToken(rawToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is { RevokedAt: null })
        {
            stored.Revoke(now);
            await db.SaveChangesAsync(ct);
        }
    }

    private AuthResult IssueTokens(User user, DateTime now)
    {
        var access = tokens.CreateAccessToken(user, now);
        var refresh = tokens.CreateRefreshToken(now);
        db.RefreshTokens.Add(RefreshToken.Create(user.Id, refresh.Hash, refresh.ExpiresAt, now));

        return new AuthResult(new AuthResponse(access.Value, access.ExpiresAt, user.Email), refresh.Raw, refresh.ExpiresAt);
    }

    private async Task RevokeAllAsync(Guid userId, DateTime now, CancellationToken ct)
    {
        var active = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now)
            .ToListAsync(ct);

        foreach (var token in active)
        {
            token.Revoke(now);
        }
    }

    /// <summary>Keeps the table small without a background job: housekeeping rides along with sign-in.</summary>
    private async Task RemoveLongExpiredTokensAsync(Guid userId, DateTime now, CancellationToken ct)
    {
        var cutoff = now - ExpiredTokenRetention;
        var old = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.ExpiresAt < cutoff)
            .ToListAsync(ct);

        db.RefreshTokens.RemoveRange(old);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}

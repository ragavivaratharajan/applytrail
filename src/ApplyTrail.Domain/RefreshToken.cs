namespace ApplyTrail.Domain;

public class RefreshToken
{
    private RefreshToken() { } // EF Core

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }

    /// <summary>SHA-256 of the random token. The raw value is never stored.</summary>
    public string TokenHash { get; private set; } = "";

    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }

    public bool IsActive(DateTime nowUtc) => RevokedAt is null && ExpiresAt > nowUtc;

    public static RefreshToken Create(Guid userId, string tokenHash, DateTime expiresAt, DateTime nowUtc) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        TokenHash = tokenHash,
        ExpiresAt = expiresAt,
        CreatedAt = nowUtc,
    };

    public void Revoke(DateTime nowUtc, string? replacedByTokenHash = null)
    {
        RevokedAt ??= nowUtc;
        ReplacedByTokenHash ??= replacedByTokenHash;
    }
}

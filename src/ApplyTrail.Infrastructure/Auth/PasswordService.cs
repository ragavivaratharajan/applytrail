using ApplyTrail.Application.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace ApplyTrail.Infrastructure.Auth;

/// <summary>PBKDF2 with a per-password salt, via the framework's PasswordHasher (no extra dependency).</summary>
public sealed class PasswordService : IPasswordService
{
    private static readonly PasswordHasher<object> Hasher = new();
    private static readonly object Subject = new();

    // Verified against when the email is unknown, so "no such user" costs the same time as "wrong password".
    private static readonly string DummyHash = Hasher.HashPassword(Subject, Guid.NewGuid().ToString("N"));

    public string Hash(string password) => Hasher.HashPassword(Subject, password);

    public bool Verify(string? hash, string password)
    {
        var result = Hasher.VerifyHashedPassword(Subject, hash ?? DummyHash, password);
        return hash is not null && result != PasswordVerificationResult.Failed;
    }
}

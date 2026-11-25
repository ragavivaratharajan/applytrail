using System.Buffers.Text;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ApplyTrail.Application.Abstractions;
using ApplyTrail.Domain;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ApplyTrail.Infrastructure.Auth;

public sealed class TokenService(JwtOptions options) : ITokenService
{
    private static readonly JsonWebTokenHandler Handler = new();

    private readonly SigningCredentials _credentials = new(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
        SecurityAlgorithms.HmacSha256);

    public AccessToken CreateAccessToken(User user, DateTime nowUtc)
    {
        var expires = nowUtc.AddMinutes(options.AccessTokenMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", user.Id.ToString()),
                new Claim("email", user.Email),
            ]),
            IssuedAt = nowUtc,
            NotBefore = nowUtc,
            Expires = expires,
            SigningCredentials = _credentials,
        };

        return new AccessToken(Handler.CreateToken(descriptor), expires);
    }

    /// <summary>256 random bits. Only the SHA-256 hash is stored, so a database leak does not leak sessions.</summary>
    public NewRefreshToken CreateRefreshToken(DateTime nowUtc)
    {
        var raw = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        return new NewRefreshToken(raw, HashRefreshToken(raw), nowUtc.AddDays(options.RefreshTokenDays));
    }

    public string HashRefreshToken(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}

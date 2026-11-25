using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ApplyTrail.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const int MinimumKeyLength = 32;

    public string Issuer { get; init; } = "applytrail";
    public string Audience { get; init; } = "applytrail-web";
    public required string SigningKey { get; init; }
    public int AccessTokenMinutes { get; init; } = 15;
    public int RefreshTokenDays { get; init; } = 14;

    /// <summary>
    /// Reads the "Jwt" configuration section. In Development a missing key is replaced by a random one
    /// (sign-ins end when the API restarts). Anywhere else a missing or weak key stops the app at startup,
    /// because a guessable signing key lets anyone mint tokens for any user.
    /// </summary>
    public static JwtOptions Resolve(IConfiguration configuration, IHostEnvironment environment)
    {
        var section = configuration.GetSection("Jwt");
        var key = section["SigningKey"];

        if (string.IsNullOrWhiteSpace(key) || key.Length < MinimumKeyLength)
        {
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    $"Jwt:SigningKey must be set to a random string of at least {MinimumKeyLength} characters. " +
                    "Generate one with: openssl rand -base64 48");
            }

            key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        }

        return new JwtOptions
        {
            Issuer = section["Issuer"] ?? "applytrail",
            Audience = section["Audience"] ?? "applytrail-web",
            SigningKey = key,
            AccessTokenMinutes = section.GetValue("AccessTokenMinutes", 15),
            RefreshTokenDays = section.GetValue("RefreshTokenDays", 14),
        };
    }
}

using System.ComponentModel.DataAnnotations;

namespace ApplyTrail.Application.Dtos;

public class RegisterRequest
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = "";

    [Required, StringLength(100, MinimumLength = 10, ErrorMessage = "Password must be 10 to 100 characters.")]
    public string Password { get; set; } = "";
}

public class LoginRequest
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = "";

    [Required, StringLength(100)]
    public string Password { get; set; } = "";
}

public class DeleteAccountRequest
{
    /// <summary>Re-entered so a stolen session cannot wipe the account.</summary>
    [Required, StringLength(100)]
    public string Password { get; set; } = "";
}

public record AuthResponse(string AccessToken, DateTime ExpiresAt, string Email);

/// <summary>What the controller needs to answer an auth call: the body plus the cookie to set.</summary>
public record AuthResult(AuthResponse Response, string? RefreshToken, DateTime? RefreshExpiresAt);

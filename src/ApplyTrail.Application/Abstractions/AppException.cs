namespace ApplyTrail.Application.Abstractions;

/// <summary>An expected failure that maps to an HTTP status and a safe, user-facing message.</summary>
public abstract class AppException(int statusCode, string title, string? detail = null) : Exception(title)
{
    public int StatusCode { get; } = statusCode;
    public string Title { get; } = title;
    public string? Detail { get; } = detail;
}

public sealed class BadRequestException(string title, string? detail = null) : AppException(400, title, detail);

public sealed class UnauthorizedException(string title = "Authentication is required.") : AppException(401, title);

public sealed class ForbiddenException(string title) : AppException(403, title);

public sealed class NotFoundException(string title = "Not found.") : AppException(404, title);

public sealed class ConflictException(string title, string? detail = null) : AppException(409, title, detail);

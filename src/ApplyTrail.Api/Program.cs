using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using ApplyTrail.Api.Infrastructure;
using ApplyTrail.Application;
using ApplyTrail.Application.Abstractions;
using ApplyTrail.Infrastructure;
using ApplyTrail.Infrastructure.Auth;
using ApplyTrail.Infrastructure.Persistence;
using ApplyTrail.Infrastructure.Seeding;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure();
builder.Services.AddApplication();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        // Enums travel as readable names ("Interviewing"), and a bare number is rejected rather than stored.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false));
    });

// ---- Authentication: short-lived JWT access tokens; the refresh token lives in an HttpOnly cookie (AuthController) ----
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<JwtOptions>((bearer, jwt) =>
    {
        bearer.MapInboundClaims = false; // keep "sub" as "sub"
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

// Secure by default: every endpoint needs a signed-in user unless it opts out with [AllowAnonymous].
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// ---- Brute-force protection for sign-in, sign-up and refresh ----
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext =>
    {
        var permits = httpContext.RequestServices.GetRequiredService<IConfiguration>()
            .GetValue("RateLimiting:AuthPermitsPerMinute", 10);

        return RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permits, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });
    });
});

// ---- Errors and API docs ----
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi();

// Nothing in this API needs a large request body.
builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = 256 * 1024);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.CacheControl = "no-store"; // responses hold personal data and tokens
    await next();
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous(); // interactive API docs at /scalar
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

// Fail fast: a missing or weak JWT signing key must stop the app at startup, not at the first sign-in.
_ = app.Services.GetRequiredService<JwtOptions>();

await DatabaseInitializer.InitializeAsync(app.Services);
if (app.Configuration.GetValue<bool>("Seed:DemoData"))
{
    await DemoDataSeeder.SeedAsync(app.Services);
}

app.Run();

// Lets the integration tests start the app in-process.
public partial class Program;

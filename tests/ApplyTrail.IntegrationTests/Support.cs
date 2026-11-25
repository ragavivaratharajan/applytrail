using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApplyTrail.Application.Dtos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ApplyTrail.IntegrationTests;

/// <summary>
/// Starts the real API in-process on a throw-away SQLite file. Each test class gets its own database.
/// The clock starts at the real current time (JWT validation uses real time) and can be nudged forward.
/// Do not advance it by more than ~30 s before using an access token issued afterwards.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbFile = Path.Combine(Path.GetTempPath(), $"applytrail-it-{Guid.NewGuid():N}.db");

    public TestClock Clock { get; } = new(DateTimeOffset.UtcNow);

    public virtual Dictionary<string, string?> Settings => new()
    {
        ["ConnectionStrings:Default"] = $"Data Source={_dbFile}",
        ["Jwt:SigningKey"] = "integration-test-signing-key-0123456789-abcdef",
        ["Auth:CookieSecure"] = "false", // the test server is plain http
        ["Auth:AllowRegistration"] = "true",
        ["Seed:DemoData"] = "false",
        ["RateLimiting:AuthPermitsPerMinute"] = "100000",
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(Settings));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        foreach (var file in new[] { _dbFile, _dbFile + "-wal", _dbFile + "-shm" })
        {
            try { File.Delete(file); } catch (IOException) { /* still open on some platforms; the OS temp cleaner gets it */ }
        }
    }
}

/// <summary>An API with a tiny sign-in rate limit, to prove the limiter is wired to the auth endpoints.</summary>
public sealed class RateLimitedApiFactory : ApiFactory
{
    public override Dictionary<string, string?> Settings
    {
        get
        {
            var settings = base.Settings;
            settings["RateLimiting:AuthPermitsPerMinute"] = "3";
            return settings;
        }
    }
}

public sealed class TestClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan by) => _now += by;
}

public sealed record TestUser(HttpClient Client, string Email, string AccessToken);

public static class Api
{
    public const string Password = "correct-horse-battery";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public static async Task<TestUser> RegisterAsync(this ApiFactory factory, string? email = null)
    {
        var client = factory.CreateClient(); // keeps cookies, like a browser
        email ??= $"user-{Guid.NewGuid():N}@example.com";

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest { Email = email, Password = Password });
        await response.ExpectAsync(HttpStatusCode.Created);

        var auth = await response.ReadAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return new TestUser(client, email, auth.AccessToken);
    }

    public static Task<HttpResponseMessage> PostJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PostAsJsonAsync(url, body, Json);

    public static Task<HttpResponseMessage> PutJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PutAsJsonAsync(url, body, Json);

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;

    /// <summary>Asserts the status code and, when it is wrong, shows the request and the response body.</summary>
    public static async Task<HttpResponseMessage> ExpectAsync(this HttpResponseMessage response, HttpStatusCode expected)
    {
        if (response.StatusCode != expected)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new Xunit.Sdk.XunitException(
                $"Expected {(int)expected} {expected} but got {(int)response.StatusCode} {response.StatusCode} " +
                $"for {response.RequestMessage?.Method} {response.RequestMessage?.RequestUri?.PathAndQuery}\n{body}");
        }

        return response;
    }

    public static async Task<ApplicationDto> CreateAppAsync(
        this TestUser user, string company, string role = "Software Engineer",
        Action<CreateApplicationRequest>? configure = null)
    {
        var request = new CreateApplicationRequest { Company = company, RoleTitle = role, Today = Today };
        configure?.Invoke(request);

        var response = await user.Client.PostJsonAsync("/api/applications", request);
        await response.ExpectAsync(HttpStatusCode.Created);
        return await response.ReadAsync<ApplicationDto>();
    }

    /// <summary>The refresh cookie's value from a response, or null when the response does not set one.</summary>
    public static string? RefreshCookie(this HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            return null;
        }

        var header = values.FirstOrDefault(v => v.StartsWith("applytrail_refresh=", StringComparison.Ordinal));
        return header?["applytrail_refresh=".Length..].Split(';')[0];
    }

    public static string? RawSetCookie(this HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith("applytrail_refresh=", StringComparison.Ordinal))
            : null;

    /// <summary>A client that does NOT keep cookies, so a test can present exactly the cookie it chooses.</summary>
    public static HttpClient CookielessClient(this ApiFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    public static Task<HttpResponseMessage> RefreshWithCookieAsync(this HttpClient client, string cookieValue)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add("Cookie", $"applytrail_refresh={cookieValue}");
        return client.SendAsync(request);
    }
}

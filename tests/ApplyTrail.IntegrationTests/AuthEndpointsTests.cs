using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ApplyTrail.Application.Dtos;

namespace ApplyTrail.IntegrationTests;

public class AuthEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<string> ErrorWithoutTraceIdAsync(HttpResponseMessage response)
    {
        var body = System.Text.Json.Nodes.JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        body.Remove("traceId");
        return body.ToJsonString();
    }

    [Fact]
    public async Task Health_is_public_and_responses_carry_security_headers()
    {
        var response = await factory.CreateClient().GetAsync("/api/health");

        await response.ExpectAsync(HttpStatusCode.OK);
        Assert.Contains("nosniff", response.Headers.GetValues("X-Content-Type-Options"));
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Protected_endpoints_reject_missing_and_forged_tokens()
    {
        var client = factory.CreateClient();
        await (await client.GetAsync("/api/applications")).ExpectAsync(HttpStatusCode.Unauthorized);
        await (await client.GetAsync("/api/me")).ExpectAsync(HttpStatusCode.Unauthorized);
        await (await client.GetAsync("/api/dashboard")).ExpectAsync(HttpStatusCode.Unauthorized);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.jwt");
        await (await client.GetAsync("/api/applications")).ExpectAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Registering_sets_an_http_only_strict_cookie_scoped_to_the_auth_endpoints()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = $"cookie-{Guid.NewGuid():N}@example.com", Password = Api.Password });

        await response.ExpectAsync(HttpStatusCode.Created);
        var cookie = response.RawSetCookie()?.ToLowerInvariant();
        Assert.NotNull(cookie);
        Assert.Contains("httponly", cookie);
        Assert.Contains("samesite=strict", cookie);
        Assert.Contains("path=/api/auth", cookie);
    }

    [Fact]
    public async Task Weak_passwords_and_bad_emails_are_rejected_with_field_errors()
    {
        var client = factory.CreateClient();

        var weak = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest { Email = "a@example.com", Password = "short" });
        await weak.ExpectAsync(HttpStatusCode.BadRequest);
        Assert.Contains("password", await weak.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var badEmail = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest { Email = "nope", Password = Api.Password });
        await badEmail.ExpectAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Registering_the_same_email_twice_is_a_conflict_even_with_different_casing()
    {
        var email = $"dup-{Guid.NewGuid():N}@example.com";
        await factory.RegisterAsync(email);

        var again = await factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = email.ToUpperInvariant(), Password = Api.Password });

        await again.ExpectAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Login_succeeds_with_the_right_password_and_gives_the_same_error_for_wrong_password_and_unknown_user()
    {
        var user = await factory.RegisterAsync();
        var client = factory.CreateClient();

        var ok = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = user.Email, Password = Api.Password });
        await ok.ExpectAsync(HttpStatusCode.OK);

        var wrong = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = user.Email, Password = "wrong-password!" });
        var unknown = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = "nobody@example.com", Password = Api.Password });
        await wrong.ExpectAsync(HttpStatusCode.Unauthorized);
        await unknown.ExpectAsync(HttpStatusCode.Unauthorized);
        // Same error for both, apart from the per-request traceId.
        Assert.Equal(await ErrorWithoutTraceIdAsync(wrong), await ErrorWithoutTraceIdAsync(unknown));
    }

    [Fact]
    public async Task A_valid_access_token_opens_protected_endpoints()
    {
        var user = await factory.RegisterAsync();

        var me = await (await user.Client.GetAsync("/api/me")).ExpectAsync(HttpStatusCode.OK);

        Assert.Equal(user.Email, (await me.ReadAsync<UserSettingsDto>()).Email);
    }

    [Fact]
    public async Task Refreshing_with_the_cookie_rotates_it()
    {
        var client = factory.CreateClient();
        var register = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = $"rot-{Guid.NewGuid():N}@example.com", Password = Api.Password });
        var first = register.RefreshCookie();

        var refreshed = await client.PostAsync("/api/auth/refresh", content: null);

        await refreshed.ExpectAsync(HttpStatusCode.OK);
        var second = refreshed.RefreshCookie();
        Assert.False(string.IsNullOrEmpty(first));
        Assert.False(string.IsNullOrEmpty(second));
        Assert.NotEqual(first, second);
        Assert.False(string.IsNullOrEmpty((await refreshed.ReadAsync<AuthResponse>()).AccessToken));
    }

    [Fact]
    public async Task Refreshing_without_a_cookie_is_unauthorized()
    {
        var response = await factory.CreateClient().PostAsync("/api/auth/refresh", content: null);

        await response.ExpectAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Reusing_a_rotated_refresh_token_after_the_grace_window_ends_every_session()
    {
        var browser = factory.CreateClient();
        var register = await browser.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = $"reuse-{Guid.NewGuid():N}@example.com", Password = Api.Password });
        var stolen = register.RefreshCookie()!;

        var attacker = factory.CookielessClient();
        var rotated = await attacker.RefreshWithCookieAsync(stolen);
        await rotated.ExpectAsync(HttpStatusCode.OK);
        var legitimateNext = rotated.RefreshCookie()!;

        factory.Clock.Advance(TimeSpan.FromSeconds(11)); // past the 10 s race-tolerance window

        await (await attacker.RefreshWithCookieAsync(stolen)).ExpectAsync(HttpStatusCode.Unauthorized);
        await (await attacker.RefreshWithCookieAsync(legitimateNext)).ExpectAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Two_tabs_refreshing_at_once_are_tolerated_inside_the_grace_window()
    {
        var browser = factory.CreateClient();
        var register = await browser.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = $"tabs-{Guid.NewGuid():N}@example.com", Password = Api.Password });
        var cookie = register.RefreshCookie()!;

        var client = factory.CookielessClient();
        await (await client.RefreshWithCookieAsync(cookie)).ExpectAsync(HttpStatusCode.OK);
        var second = await client.RefreshWithCookieAsync(cookie); // the second tab, same cookie, moments later

        await second.ExpectAsync(HttpStatusCode.OK);
        Assert.Null(second.RefreshCookie()); // it gets an access token but must NOT overwrite the winner's cookie
    }

    [Fact]
    public async Task Logging_out_revokes_the_session_and_clears_the_cookie()
    {
        var client = factory.CreateClient();
        var register = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = $"out-{Guid.NewGuid():N}@example.com", Password = Api.Password });
        var cookie = register.RefreshCookie()!;

        var logout = await client.PostAsync("/api/auth/logout", content: null);

        await logout.ExpectAsync(HttpStatusCode.NoContent);
        await (await factory.CookielessClient().RefreshWithCookieAsync(cookie)).ExpectAsync(HttpStatusCode.Unauthorized);
    }
}

public class RateLimitTests(RateLimitedApiFactory factory) : IClassFixture<RateLimitedApiFactory>
{
    [Fact]
    public async Task Repeated_sign_in_attempts_are_throttled()
    {
        var client = factory.CreateClient();
        var login = new LoginRequest { Email = "nobody@example.com", Password = "wrong-password!" };

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 6; i++)
        {
            statuses.Add((await client.PostAsJsonAsync("/api/auth/login", login)).StatusCode);
        }

        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
        Assert.Equal(HttpStatusCode.Unauthorized, statuses[0]);
    }
}

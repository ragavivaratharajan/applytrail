using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ApplyTrail.Application.Dtos;

namespace ApplyTrail.IntegrationTests;

public class ExportAccountTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task The_csv_export_is_a_download_that_excel_reads_correctly_and_cannot_run_formulas()
    {
        var user = await factory.RegisterAsync();
        await user.CreateAppAsync("=HYPERLINK(\"http://evil\")", "Dev, Senior", r => { r.Notes = "-1 line"; r.Location = "Café, Dublin"; });

        var response = await user.Client.GetAsync("/api/export/csv");

        await response.ExpectAsync(HttpStatusCode.OK);
        Assert.StartsWith("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.EndsWith(".csv", response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]); // UTF-8 byte-order mark
        var csv = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        Assert.StartsWith("Company,Role,Status,", csv);
        Assert.Contains("\"'=HYPERLINK(\"\"http://evil\"\")\"", csv); // leading '=' neutralised
        Assert.Contains("\"Dev, Senior\"", csv);
        Assert.Contains("'-1 line", csv);
        Assert.Contains("\"Café, Dublin\"", csv);
    }

    [Fact]
    public async Task The_json_export_contains_everything_with_a_version()
    {
        var user = await factory.RegisterAsync();
        await user.CreateAppAsync("Acme");

        var response = await user.Client.GetAsync("/api/export/json");

        await response.ExpectAsync(HttpStatusCode.OK);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var export = JsonSerializer.Deserialize<FullExportDto>(await response.Content.ReadAsStringAsync(), Api.Json)!;
        Assert.Equal(1, export.Version);
        var exported = Assert.Single(export.Applications);
        Assert.Equal("Acme", exported.Application.Company);
        Assert.NotEmpty(exported.Events);
        Assert.NotEmpty(exported.Reminders);
    }

    [Fact]
    public async Task Deleting_the_account_needs_the_password_and_removes_everything()
    {
        var user = await factory.RegisterAsync();
        await user.CreateAppAsync("Acme");

        var wrong = new HttpRequestMessage(HttpMethod.Delete, "/api/me") { Content = JsonContent.Create(new DeleteAccountRequest { Password = "wrong-password!" }) };
        await (await user.Client.SendAsync(wrong)).ExpectAsync(HttpStatusCode.Forbidden);

        var right = new HttpRequestMessage(HttpMethod.Delete, "/api/me") { Content = JsonContent.Create(new DeleteAccountRequest { Password = Api.Password }) };
        await (await user.Client.SendAsync(right)).ExpectAsync(HttpStatusCode.NoContent);

        var login = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = user.Email, Password = Api.Password });
        await login.ExpectAsync(HttpStatusCode.Unauthorized);

        // the access token is still cryptographically valid for a few minutes, but its owner no longer exists
        await (await user.Client.GetAsync("/api/applications")).ExpectAsync(HttpStatusCode.Unauthorized);
    }
}

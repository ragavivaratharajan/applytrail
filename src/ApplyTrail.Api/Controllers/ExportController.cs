using System.Text.Json;
using ApplyTrail.Application.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ApplyTrail.Api.Controllers;

[ApiController]
[Route("api/export")]
public class ExportController(ExportService export, IOptions<Microsoft.AspNetCore.Mvc.JsonOptions> jsonOptions) : ControllerBase
{
    [HttpGet("csv")]
    public async Task<IActionResult> Csv(CancellationToken ct)
    {
        var file = await export.ExportCsvAsync(ct);
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpGet("json")]
    public async Task<IActionResult> Json(CancellationToken ct)
    {
        var data = await export.ExportJsonAsync(ct);
        var options = new JsonSerializerOptions(jsonOptions.Value.JsonSerializerOptions) { WriteIndented = true };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(data, options);
        return File(bytes, "application/json", $"applytrail-{data.ExportedAt:yyyy-MM-dd}.json");
    }
}

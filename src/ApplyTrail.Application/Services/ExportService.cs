using System.Globalization;
using System.Text;
using ApplyTrail.Application.Abstractions;
using ApplyTrail.Application.Dtos;
using ApplyTrail.Application.Logic;
using Microsoft.EntityFrameworkCore;
using static ApplyTrail.Application.Services.ServiceHelpers;

namespace ApplyTrail.Application.Services;

public sealed record ExportFile(string FileName, string ContentType, byte[] Content);

/// <summary>Your data is yours: a spreadsheet for people, JSON for backups and other tools.</summary>
public sealed class ExportService(IApplicationDbContext db, ICurrentUser currentUser, TimeProvider clock)
{
    public const int JsonVersion = 1;

    private static readonly string[] CsvHeaders =
    [
        "Company", "Role", "Status", "Applied on", "Source", "Location", "Work mode", "Salary min", "Salary max",
        "Currency", "Sponsorship", "Priority", "Job URL", "Next reminder", "Last activity (UTC)", "Archived", "Notes",
    ];

    public async Task<ExportFile> ExportCsvAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var details = await LoadAsync(now, ct);

        var rows = details
            .Select(d => d.Application)
            .OrderBy(a => a.Company, StringComparer.OrdinalIgnoreCase)
            .Select(a => (IReadOnlyList<string?>)
            [
                a.Company,
                a.RoleTitle,
                a.Status.ToString(),
                Date(a.AppliedOn),
                a.Source.ToString(),
                a.Location,
                a.WorkMode.ToString(),
                a.SalaryMin?.ToString(CultureInfo.InvariantCulture),
                a.SalaryMax?.ToString(CultureInfo.InvariantCulture),
                a.Currency,
                a.Sponsorship.ToString(),
                a.Priority.ToString(),
                a.JobUrl,
                Date(a.NextReminderOn),
                a.LastActivityAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                a.IsArchived ? "Yes" : "No",
                a.Notes,
            ]);

        var csv = CsvExporter.Write(CsvHeaders, rows);

        // The byte-order mark makes Excel read accents and the euro sign correctly.
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
        return new ExportFile($"applytrail-{Date(DateOnly.FromDateTime(now))}.csv", "text/csv; charset=utf-8", bytes);
    }

    public async Task<FullExportDto> ExportJsonAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var details = await LoadAsync(now, ct);
        return new FullExportDto(now, JsonVersion, details);
    }

    private async Task<List<ApplicationDetailDto>> LoadAsync(DateTime now, CancellationToken ct)
    {
        var userId = currentUser.Id;
        var settings = await db.GetSettingsAsync(userId, ct);

        var applications = await db.Applications.AsNoTracking()
            .Where(a => a.UserId == userId)
            .Include(a => a.Events)
            .Include(a => a.Reminders)
            .AsSplitQuery()
            .ToListAsync(ct);

        return applications.Select(a => a.ToDetailDto(now, settings.StaleAfterDays)).ToList();
    }

    private static string? Date(DateOnly? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

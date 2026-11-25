using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ApplyTrail.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    /// <summary>
    /// Applies migrations when the project has any, otherwise creates the schema from the model.
    /// (Run `dotnet ef migrations add InitialCreate` once and commit the result to switch to migrations.)
    /// </summary>
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        EnsureDirectoryExists(db.Database.GetConnectionString());

        if (db.Database.GetMigrations().Any())
        {
            await db.Database.MigrateAsync(ct);
        }
        else
        {
            await db.Database.EnsureCreatedAsync(ct);
        }

        // Write-ahead logging lets reads continue while a write is in progress. The setting is stored in the file.
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", ct);
    }

    private static void EnsureDirectoryExists(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
        var directory = string.IsNullOrWhiteSpace(dataSource) ? null : Path.GetDirectoryName(Path.GetFullPath(dataSource));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}

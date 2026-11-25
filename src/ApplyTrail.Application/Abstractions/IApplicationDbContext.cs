using ApplyTrail.Domain;
using Microsoft.EntityFrameworkCore;

namespace ApplyTrail.Application.Abstractions;

/// <summary>The slice of EF Core the application layer needs. Implemented by the Infrastructure DbContext.</summary>
public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<JobApplication> Applications { get; }
    DbSet<ActivityEvent> Events { get; }
    DbSet<Reminder> Reminders { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

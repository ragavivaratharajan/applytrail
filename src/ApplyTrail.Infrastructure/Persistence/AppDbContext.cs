using ApplyTrail.Application.Abstractions;
using ApplyTrail.Domain;
using Microsoft.EntityFrameworkCore;

namespace ApplyTrail.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IApplicationDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<JobApplication> Applications => Set<JobApplication>();
    public DbSet<ActivityEvent> Events => Set<ActivityEvent>();
    public DbSet<Reminder> Reminders => Set<Reminder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Every key is assigned by the domain (Guid.NewGuid). Without ValueGeneratedNever, EF treats a new child with a
        // non-empty Guid as an EXISTING row and issues an UPDATE instead of an INSERT.
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Email).HasMaxLength(254).IsRequired();
            e.Property(x => x.PasswordHash).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
            e.Property(x => x.ReplacedByTokenHash).HasMaxLength(64);
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => x.UserId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<JobApplication>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Company).HasMaxLength(150).IsRequired();
            e.Property(x => x.RoleTitle).HasMaxLength(150).IsRequired();
            e.Property(x => x.JobUrl).HasMaxLength(2000);
            e.Property(x => x.Location).HasMaxLength(150);
            e.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            e.Property(x => x.Notes).HasMaxLength(5000);

            // Enums are stored as readable text so the database is easy to inspect and survives reordering.
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Source).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.WorkMode).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Sponsorship).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Priority).HasConversion<string>().HasMaxLength(20);

            e.HasIndex(x => new { x.UserId, x.Status, x.ArchivedAt });
            e.HasIndex(x => new { x.UserId, x.LastActivityAt });

            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Events).WithOne().HasForeignKey(x => x.JobApplicationId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Reminders).WithOne().HasForeignKey(x => x.JobApplicationId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ActivityEvent>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Title).HasMaxLength(150).IsRequired();
            e.Property(x => x.Details).HasMaxLength(2000);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => new { x.JobApplicationId, x.OccurredAt });
        });

        modelBuilder.Entity<Reminder>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Title).HasMaxLength(150).IsRequired();
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => new { x.JobApplicationId, x.DueOn });
        });
    }
}

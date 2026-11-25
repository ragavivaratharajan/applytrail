using ApplyTrail.Application.Abstractions;
using ApplyTrail.Application.Dtos;
using ApplyTrail.Domain;
using Microsoft.EntityFrameworkCore;

namespace ApplyTrail.Application.Services;

public sealed class SettingsService(IApplicationDbContext db, ICurrentUser currentUser, IPasswordService passwords)
{
    public async Task<UserSettingsDto> GetAsync(CancellationToken ct)
    {
        var user = await LoadUserAsync(tracking: false, ct);
        return ToDto(user);
    }

    public async Task<UserSettingsDto> UpdateAsync(UpdateSettingsRequest request, CancellationToken ct)
    {
        var user = await LoadUserAsync(tracking: true, ct);
        user.UpdateSettings(request.WeeklyGoal, request.FollowUpAfterDays, request.StaleAfterDays);

        await db.SaveChangesAsync(ct);
        return ToDto(user);
    }

    /// <summary>Permanently deletes the account and everything in it (applications, events, reminders, sessions).</summary>
    public async Task DeleteAccountAsync(DeleteAccountRequest request, CancellationToken ct)
    {
        var user = await LoadUserAsync(tracking: true, ct);

        if (!passwords.Verify(user.PasswordHash, request.Password))
        {
            throw new ForbiddenException("The password is incorrect.");
        }

        db.Users.Remove(user); // cascades to everything the user owns
        await db.SaveChangesAsync(ct);
    }

    private async Task<User> LoadUserAsync(bool tracking, CancellationToken ct)
    {
        var userId = currentUser.Id;
        var query = tracking ? db.Users.AsQueryable() : db.Users.AsNoTracking();
        return await query.FirstOrDefaultAsync(u => u.Id == userId, ct) ?? throw new UnauthorizedException();
    }

    private static UserSettingsDto ToDto(User user) =>
        new(user.Email, user.WeeklyGoal, user.FollowUpAfterDays, user.StaleAfterDays);
}

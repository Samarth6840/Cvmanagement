using CvManagement.Data;
using CvManagement.Data.Entities.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Services.Admin;

/// <summary>One row in the administrator's user table (spec §3.7).</summary>
public record AdminUserSummary(
    Guid Id,
    string DisplayName,
    string Email,
    bool IsBlocked,
    DateTimeOffset CreatedAt,
    IReadOnlyList<string> Roles);

/// <summary>
/// User management for administrators: view, block/unblock, delete, and assign/remove roles
/// (spec §3.7). The rules about what an administrator may do to their own account live here
/// rather than in the page, so they hold regardless of which UI calls them.
/// </summary>
public interface IAdminUserService
{
    Task<List<AdminUserSummary>> ListUsersAsync();

    /// <summary>Blocks or unblocks every user in <paramref name="targetUserIds"/>.</summary>
    Task SetBlockedManyAsync(Guid actingUserId, IReadOnlyCollection<Guid> targetUserIds, bool blocked);

    Task DeleteManyAsync(Guid actingUserId, IReadOnlyCollection<Guid> targetUserIds);

    Task SetRoleAsync(Guid actingUserId, Guid targetUserId, string role, bool assigned);
}

public class AdminUserService : IAdminUserService
{
    // Indefinite block. Lockout compares this against UtcNow, so far-future means "until
    // an administrator lifts it".
    private static readonly DateTimeOffset IndefiniteLockout = DateTimeOffset.MaxValue;

    private const string CannotActOnSelfMessage = "Administrators cannot block or delete their own account.";
    private const string UnknownRoleMessage = "Unknown role.";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly CvDbContext _db;

    public AdminUserService(UserManager<ApplicationUser> userManager, CvDbContext db)
    {
        _userManager = userManager;
        _db = db;
    }

    public async Task<List<AdminUserSummary>> ListUsersAsync()
    {
        var users = await _db.Users
            .AsNoTracking()
            .OrderBy(u => u.DisplayName)
            .Select(u => new
            {
                u.Id,
                u.DisplayName,
                u.Email,
                u.LockoutEnd,
                u.CreatedAt
            })
            .ToListAsync();


        // Roles are fetched per user by Identity (its own tables), but that is a fixed two
        // round-trips per listed row rather than a query buried in an unrelated loop.
        var summaries = new List<AdminUserSummary>(users.Count);
        foreach (var user in users)
        {
            var entity = new ApplicationUser { Id = user.Id, UserName = user.Email, Email = user.Email };
            var roles = await _userManager.GetRolesAsync(entity);

            summaries.Add(new AdminUserSummary(
                user.Id,
                user.DisplayName,
                user.Email ?? string.Empty,
                user.LockoutEnd > DateTimeOffset.UtcNow,
                user.CreatedAt,
                roles.OrderBy(r => r).ToList()));
        }

        return summaries;
    }

    public async Task SetBlockedManyAsync(
        Guid actingUserId,
        IReadOnlyCollection<Guid> targetUserIds,
        bool blocked)
    {
        foreach (var user in await LoadTargetsAsync(actingUserId, targetUserIds))
        {
            var result = await _userManager.SetLockoutEndDateAsync(
                user, blocked ? IndefiniteLockout : null);
            if (!result.Succeeded)
                throw new InvalidOperationException(Describe(result));
        }
    }

    public async Task DeleteManyAsync(Guid actingUserId, IReadOnlyCollection<Guid> targetUserIds)
    {
        foreach (var user in await LoadTargetsAsync(actingUserId, targetUserIds))
        {
            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
                throw new InvalidOperationException(Describe(result));
        }
    }

    // One query for the whole selection. The administrator's own row is excluded here, in
    // the same place the rule is documented, rather than trusted to the caller.
    private async Task<List<ApplicationUser>> LoadTargetsAsync(
        Guid actingUserId,
        IReadOnlyCollection<Guid> targetUserIds)
    {
        var ids = targetUserIds.Where(id => id != actingUserId).ToList();

        if (ids.Count != targetUserIds.Count)
            throw new InvalidOperationException(CannotActOnSelfMessage);

        if (ids.Count == 0)
            return [];

        return await _db.Users.Where(u => ids.Contains(u.Id)).ToListAsync();
    }

    private static string Describe(IdentityResult result) =>
        string.Join(" ", result.Errors.Select(e => e.Description));

    // Self-demotion is explicitly allowed (spec §3.7): an administrator may strip their own
    // Administrator role, so the self-check only blocks the destructive account actions.
    public async Task SetRoleAsync(Guid actingUserId, Guid targetUserId, string role, bool assigned)
    {
        if (!RoleNames.All.Contains(role))
            throw new InvalidOperationException(UnknownRoleMessage);

        var user = await FindAsync(targetUserId);

        if (assigned)
        {
            if (await _userManager.IsInRoleAsync(user, role))
                return;

            var result = await _userManager.AddToRoleAsync(user, role);
            if (!result.Succeeded)
                throw new InvalidOperationException(Describe(result));
            return;
        }

        var removeResult = await _userManager.RemoveFromRoleAsync(user, role);
        if (!removeResult.Succeeded)
            throw new InvalidOperationException(Describe(removeResult));
    }

    private async Task<ApplicationUser> FindAsync(Guid userId) =>
        await _db.Users.FirstOrDefaultAsync(u => u.Id == userId)
        ?? throw new InvalidOperationException("User not found.");
}

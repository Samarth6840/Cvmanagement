using System.Security.Claims;
using CvManagement.Data;
using CvManagement.Data.Entities.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Services.Auth;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly CvDbContext _db;

    public CurrentUserService(
        IHttpContextAccessor httpContextAccessor,
        UserManager<ApplicationUser> userManager,
        CvDbContext db)
    {
        _httpContextAccessor = httpContextAccessor;
        _userManager = userManager;
        _db = db;
    }

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    public async Task<Guid?> GetUserIdAsync()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true) return null;

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return userId is not null ? Guid.Parse(userId) : null;
    }

    public async Task<string?> GetUserDisplayNameAsync()
    {
        var userId = await GetUserIdAsync();
        if (userId is null) return null;

        var user = await _userManager.FindByIdAsync(userId.Value.ToString());
        return user?.DisplayName;
    }

    public async Task<bool> IsInRoleAsync(string role)
    {
        var userId = await GetUserIdAsync();
        if (userId is null) return false;

        var user = await _userManager.FindByIdAsync(userId.Value.ToString());
        return user is not null && await _userManager.IsInRoleAsync(user, role);
    }

    public async Task<string> GetPreferredLanguageAsync()
    {
        var userId = await GetUserIdAsync();
        if (userId is null) return "en";

        var user = await _db.Users.FindAsync(userId.Value);
        return user?.PreferredLanguage ?? "en";
    }

    public async Task<string> GetPreferredThemeAsync()
    {
        var userId = await GetUserIdAsync();
        if (userId is null) return "light";

        var user = await _db.Users.FindAsync(userId.Value);
        return user?.PreferredTheme ?? "light";
    }
}

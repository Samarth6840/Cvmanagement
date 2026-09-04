using System.Security.Claims;

namespace CvManagement.Services.Auth;

public interface ICurrentUserService
{
    Task<Guid?> GetUserIdAsync();
    Task<string?> GetUserDisplayNameAsync();
    Task<bool> IsInRoleAsync(string role);
    Task<string> GetPreferredLanguageAsync();
    Task<string> GetPreferredThemeAsync();
    bool IsAuthenticated { get; }
}

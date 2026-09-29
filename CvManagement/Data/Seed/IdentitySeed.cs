using Microsoft.AspNetCore.Identity;
using CvManagement.Data.Entities.Identity;

namespace CvManagement.Data.Seed;

public static class IdentitySeed
{
    public const string AdminEmailConfigKey = "Seed:AdminEmail";
    public const string AdminPasswordConfigKey = "Seed:AdminPassword";

    public static async Task SeedAsync(IServiceProvider sp)
    {
        var roleManager = sp.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var config = sp.GetRequiredService<IConfiguration>();

        await SeedRolesAsync(roleManager);
        await SeedAdministratorAsync(userManager, config);
    }

    private static async Task SeedRolesAsync(RoleManager<ApplicationRole> roleManager)
    {
        foreach (var role in RoleNames.All)
        {
            if (await roleManager.RoleExistsAsync(role))
                continue;

            await roleManager.CreateAsync(new ApplicationRole
            {
                Name = role,
                NormalizedName = RoleNames.ToNormalized(role),
                Description = role
            });
        }
    }

    // No default account unless an operator supplies credentials via configuration.
    // A committed seed password would be a live admin login in every deployed environment.
    private static async Task SeedAdministratorAsync(
        UserManager<ApplicationUser> userManager,
        IConfiguration config)
    {
        var email = config[AdminEmailConfigKey];
        var password = config[AdminPasswordConfigKey];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return;

        if (await userManager.FindByEmailAsync(email) is not null)
            return;

        // UserManager normalizes UserName/Email itself.
        var admin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = "System Admin",
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(admin, password);
        if (result.Succeeded)
            await userManager.AddToRoleAsync(admin, RoleNames.Administrator);
    }
}

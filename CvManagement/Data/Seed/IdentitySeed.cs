using Microsoft.AspNetCore.Identity;
using CvManagement.Data.Entities.Identity;

namespace CvManagement.Data.Seed;

public static class IdentitySeed
{
    public static async Task SeedAsync(IServiceProvider sp)
    {
        var roleManager = sp.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();

        string[] roles = ["Candidate", "Recruiter", "Administrator"];

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new ApplicationRole
                {
                    Name = role,
                    NormalizedName = role.ToUpperInvariant(),
                    Description = role
                });
            }
        }

        const string adminEmail = "admin@cvmanagement.local";
        if (await userManager.FindByEmailAsync(adminEmail) is null)
        {
            var admin = new ApplicationUser
            {
                UserName = adminEmail,
                NormalizedUserName = adminEmail.ToUpperInvariant(),
                Email = adminEmail,
                NormalizedEmail = adminEmail.ToUpperInvariant(),
                DisplayName = "System Admin",
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(admin, "Admin@12345");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, "Administrator");
            }
        }
    }
}

using System.Security.Claims;
using AspNet.Security.OAuth.GitHub;
using CvManagement.Components;
using CvManagement.Data;
using CvManagement.Data.Seed;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CvManagement.Data.Entities.Identity;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "";
if (Uri.TryCreate(connectionString, UriKind.Absolute, out var uri) &&
    uri.Scheme is "postgres" or "postgresql")
{
    var info = uri.UserInfo.Split(':', 2);
    connectionString = $"Host={uri.Host};Port={(uri.IsDefaultPort ? 5432 : uri.Port)};Database={uri.AbsolutePath.TrimStart('/')};" +
                       $"Username={Uri.UnescapeDataString(info[0])};" +
                       $"Password={Uri.UnescapeDataString(info.Length > 1 ? info[1] : "")};" +
                       "SSL Mode=Require";
}

builder.Services.AddDbContext<CvDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.SignIn.RequireConfirmedAccount = false;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<CvDbContext>()
.AddDefaultTokenProviders();

var authBuilder = builder.Services.AddAuthentication();

var googleId = builder.Configuration["Authentication:Google:ClientId"];
var googleSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleId))
{
    authBuilder.AddGoogle(o =>
    {
        o.ClientId = googleId;
        o.ClientSecret = googleSecret ?? "";
        o.SignInScheme = IdentityConstants.ExternalScheme;
    });
}

var gitHubId = builder.Configuration["Authentication:GitHub:ClientId"];
var gitHubSecret = builder.Configuration["Authentication:GitHub:ClientSecret"];
if (!string.IsNullOrWhiteSpace(gitHubId))
{
    authBuilder.AddGitHub(o =>
    {
        o.ClientId = gitHubId;
        o.ClientSecret = gitHubSecret ?? "";
        o.SignInScheme = IdentityConstants.ExternalScheme;
    });
}

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("RecruiterOnly", p => p.RequireRole("Recruiter", "Administrator"))
    .AddPolicy("AdminOnly", p => p.RequireRole("Administrator"))
    .AddPolicy("CandidateOnly", p => p.RequireRole("Candidate"));

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<CvManagement.Services.Auth.ICurrentUserService, CvManagement.Services.Auth.CurrentUserService>();
builder.Services.AddScoped<CvManagement.Services.Attributes.IAttributeService, CvManagement.Services.Attributes.AttributeService>();
builder.Services.AddScoped<CvManagement.Services.Profiles.IProfileService, CvManagement.Services.Profiles.ProfileService>();
builder.Services.AddScoped<CvManagement.Services.Markdown.IMarkdownRenderer, CvManagement.Services.Markdown.MarkdownRenderer>();
builder.Services.AddScoped<CvManagement.Services.Positions.IPositionService, CvManagement.Services.Positions.PositionService>();
builder.Services.AddScoped<CvManagement.Services.Projects.IProjectService, CvManagement.Services.Projects.ProjectService>();
builder.Services.AddScoped<CvManagement.Services.Cv.ICvService, CvManagement.Services.Cv.CvService>();
builder.Services.AddScoped<CvManagement.Services.Likes.ILikeService, CvManagement.Services.Likes.LikeService>();
builder.Services.AddScoped<CvManagement.Services.Search.ISearchService, CvManagement.Services.Search.SearchService>();
builder.Services.AddScoped<CvManagement.Services.Discussions.IDiscussionService, CvManagement.Services.Discussions.DiscussionService>();

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.AddSignalR();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapHub<CvManagement.Hubs.DiscussionHub>("/hubs/discussion");

var externalSchemas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    ["Google"] = GoogleDefaults.AuthenticationScheme,
    ["GitHub"] = GitHubAuthenticationDefaults.AuthenticationScheme,
};

app.MapGet("/external-login/{provider}", (string provider, HttpContext ctx, IConfiguration config) =>
{
    var scheme = externalSchemas.GetValueOrDefault(provider);
    var clientId = config[$"Authentication:{provider}:ClientId"];
    if (scheme is null || string.IsNullOrEmpty(clientId))
        return Results.Redirect("/login?error=provider-not-configured");

    return Results.Challenge(new AuthenticationProperties
    {
        RedirectUri = "/external-callback"
    }, new[] { scheme });
});

app.MapGet("/external-callback", async (HttpContext ctx, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager) =>
{
    var info = await signInManager.GetExternalLoginInfoAsync();
    if (info is null)
        return Results.Redirect("/login?error=external-failed");

    if (signInManager.IsSignedIn(ctx.User))
    {
        var existing = await userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
        if (existing is not null)
            return Results.Redirect("/");

        var current = await userManager.GetUserAsync(ctx.User);
        var link = await userManager.AddLoginAsync(current, info);
        return Results.Redirect(link.Succeeded ? "/" : "/login?error=link-failed");
    }

    var result = await signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false);
    if (result.Succeeded)
        return Results.Redirect("/");

    var email = info.Principal.FindFirstValue(ClaimTypes.Email);
    if (string.IsNullOrEmpty(email))
        return Results.Redirect("/login?error=no-email");

    var user = await userManager.FindByEmailAsync(email);
    if (user is null)
    {
        user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = info.Principal.FindFirstValue(ClaimTypes.Name) ?? email,
            EmailConfirmed = true
        };
        var created = await userManager.CreateAsync(user);
        if (!created.Succeeded)
            return Results.Redirect("/login?error=account-failed");

        var roleResult = await userManager.AddToRoleAsync(user, "Candidate");
        if (!roleResult.Succeeded)
            return Results.Redirect("/login?error=role-failed");
    }

    var addLogin = await userManager.AddLoginAsync(user, info);
    if (!addLogin.Succeeded)
        return Results.Redirect("/login?error=link-failed");

    await signInManager.SignInAsync(user, isPersistent: false);
    return Results.Redirect("/");
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CvDbContext>();
    await db.Database.MigrateAsync();
    await IdentitySeed.SeedAsync(scope.ServiceProvider);
}

app.Run();

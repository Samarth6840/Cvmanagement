using System.Globalization;
using System.Security.Claims;
using AspNet.Security.OAuth.GitHub;
using CvManagement.Components;
using CvManagement.Data;
using CvManagement.Data.Seed;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
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
    options.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<CvDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/access-denied";
});

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
    .AddPolicy(AuthorizationPolicies.RecruiterOnly,
        p => p.RequireRole(RoleNames.Recruiter, RoleNames.Administrator))
    .AddPolicy(AuthorizationPolicies.AdminOnly, p => p.RequireRole(RoleNames.Administrator))
    .AddPolicy(AuthorizationPolicies.CandidateOnly, p => p.RequireRole(RoleNames.Candidate));

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
builder.Services.AddScoped<CvManagement.Services.Admin.IAdminUserService, CvManagement.Services.Admin.AdminUserService>();

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

const string CultureCookieName = ".AspNetCore.Culture";
var supportedCultures = CvManagement.SupportedLanguages.All.Select(l => new CultureInfo(l.Code)).ToList();

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture(supportedCultures[0]);
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.RequestCultureProviders = [new CookieRequestCultureProvider()];
    options.ApplyCurrentCultureToResponseHeaders = true;
});

builder.Services.AddSignalR();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRequestLocalization();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/culture/set", async (
    HttpContext context,
    IAntiforgery antiforgery,
    [FromForm] string culture,
    [FromForm] string? redirectUri) =>
{
    await antiforgery.ValidateRequestAsync(context);

    var code = CvManagement.SupportedLanguages.Normalize(culture);
    context.Response.Cookies.Append(CultureCookieName, CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(code)), new CookieOptions
    {
        Path = "/",
        HttpOnly = true,
        IsEssential = true,
        SameSite = SameSiteMode.Lax,
        Expires = DateTimeOffset.UtcNow.AddYears(1)
    });

    var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId is not null && Guid.TryParse(userId, out var id))
    {
        await using var scope = context.RequestServices.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(id.ToString());
        if (user is not null && user.PreferredLanguage != code)
        {
            user.PreferredLanguage = code;
            await users.UpdateAsync(user);
        }
    }

    var target = !string.IsNullOrEmpty(redirectUri) && redirectUri.StartsWith('/') && !redirectUri.StartsWith("//")
        ? redirectUri
        : "/";
    return Results.LocalRedirect(target);
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapHub<CvManagement.Hubs.DiscussionHub>("/hubs/discussion")
    .RequireAuthorization();

var externalSchemas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    ["Google"] = GoogleDefaults.AuthenticationScheme,
    ["GitHub"] = GitHubAuthenticationDefaults.AuthenticationScheme,
};

app.MapGet("/external-login/{provider}", (string provider, IConfiguration config, string? returnUrl) =>
{
    var scheme = externalSchemas.GetValueOrDefault(provider);
    var clientId = config[$"Authentication:{provider}:ClientId"];
    if (scheme is null || string.IsNullOrEmpty(clientId))
        return Results.Redirect("/login?error=provider-not-configured");

    var callback = string.IsNullOrEmpty(returnUrl)
        ? "/external-callback"
        : $"/external-callback?returnUrl={Uri.EscapeDataString(returnUrl)}";

    return Results.Challenge(new AuthenticationProperties
    {
        RedirectUri = callback
    }, new[] { scheme });
});

app.MapGet("/external-callback", async (HttpContext ctx, string? returnUrl, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager) =>
{
    var target = !string.IsNullOrEmpty(returnUrl) && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//")
        ? returnUrl
        : "/";

    var info = await signInManager.GetExternalLoginInfoAsync();
    if (info is null)
        return Results.Redirect("/login?error=external-failed");

    if (signInManager.IsSignedIn(ctx.User))
    {
        var existing = await userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
        if (existing is not null)
            return Results.LocalRedirect(target);

        var current = await userManager.GetUserAsync(ctx.User);
        if (current is null)
            return Results.Redirect("/login?error=external-failed");

        var link = await userManager.AddLoginAsync(current, info);
        return link.Succeeded
            ? Results.LocalRedirect(target)
            : Results.Redirect("/login?error=link-failed");
    }

    var result = await signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false);
    if (result.Succeeded)
        return Results.LocalRedirect(target);

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

        var roleResult = await userManager.AddToRoleAsync(user, RoleNames.Candidate);
        if (!roleResult.Succeeded)
            return Results.Redirect("/login?error=role-failed");
    }

    var addLogin = await userManager.AddLoginAsync(user, info);
    if (!addLogin.Succeeded)
        return Results.Redirect("/login?error=link-failed");

    await signInManager.SignInAsync(user, isPersistent: false);
    return Results.LocalRedirect(target);
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CvDbContext>();
    await db.Database.MigrateAsync();
    await AttributeCategorySeed.SeedAsync(scope.ServiceProvider);
    await IdentitySeed.SeedAsync(scope.ServiceProvider);
    await AttributeSeed.SeedBuiltInAttributesAsync(scope.ServiceProvider);

    var seedDemoData = builder.Configuration.GetValue<bool?>("Seed:DemoData")
        ?? app.Environment.IsDevelopment();
    await DemoDataSeed.SeedAsync(scope.ServiceProvider, seedDemoData);
}

app.Run();

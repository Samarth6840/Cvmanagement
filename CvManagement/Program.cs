using CvManagement.Components;
using CvManagement.Data;
using CvManagement.Data.Seed;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CvManagement.Data.Entities.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<CvDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

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

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
})
.AddCookie(IdentityConstants.ApplicationScheme)
.AddCookie(IdentityConstants.ExternalScheme)
.AddGoogle("Google", options =>
{
    options.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? "";
    options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? "";
    options.CallbackPath = "/signin-google";
})
.AddGitHub("GitHub", options =>
{
    options.ClientId = builder.Configuration["Authentication:GitHub:ClientId"] ?? "";
    options.ClientSecret = builder.Configuration["Authentication:GitHub:ClientSecret"] ?? "";
    options.CallbackPath = "/signin-github";
    options.Scope.Add("user:email");
});

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
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(Program).Assembly);

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CvDbContext>();
    await db.Database.MigrateAsync();
    await IdentitySeed.SeedAsync(scope.ServiceProvider);
}

app.Run();

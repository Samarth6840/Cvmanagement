using CvManagement.Data.Entities.Attributes;
using CvManagement.Data.Entities.Cv;
using CvManagement.Data.Entities.Discussions;
using CvManagement.Data.Entities.Identity;
using CvManagement.Data.Entities.Likes;
using CvManagement.Data.Entities.Positions;
using CvManagement.Data.Entities.Profiles;
using CvManagement.Data.Entities.Projects;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Data.Seed;

/// <summary>
/// Sample content so a freshly created database is not three empty tables. Everything here
/// goes through the same entities and services the app uses, so the data exercises the real
/// code paths: attribute values live on the profile, a CV is a thin record, and the project
/// list on a CV is derived from the position's tags.
///
/// It only runs against an empty database, and only when enabled (Development by default, or
/// <c>Seed:DemoData=true</c>). Credentials are fixed and printed to the log on creation.
/// </summary>
public static class DemoDataSeed
{
    public const string DemoPassword = "Demo1234!";
    public const string RecruiterEmail = "recruiter@demo.local";
    public const string CandidateEmail = "candidate@demo.local";

    public static async Task SeedAsync(IServiceProvider serviceProvider, bool enabled)
    {
        if (!enabled)
            return;

        var db = serviceProvider.GetRequiredService<CvDbContext>();
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DemoDataSeed));

        // Never touch a database that already holds content. The accounts below are
        // find-or-create, so a run that failed halfway is safe to repeat.
        if (await db.Positions.AnyAsync())
            return;

        var recruiter = await EnsureUserAsync(userManager, RecruiterEmail, "Dana Recruiter", RoleNames.Recruiter);
        var candidate = await EnsureUserAsync(userManager, CandidateEmail, "Sam Candidate", RoleNames.Candidate);
        if (recruiter is null || candidate is null)
            return;

        var ielts = NewAttribute("IELTS Score", AttributeCategoryCatalog.DomainKnowledge, AttributeDataType.Numeric, 10,
            "Overall IELTS band score.");
        var remoteWork = NewAttribute("Remote Work Available", AttributeCategoryCatalog.SoftSkills, AttributeDataType.Boolean, 11,
            "Whether the candidate can work remotely.");
        var presentation = NewAttribute("Presentation Skills", AttributeCategoryCatalog.SoftSkills, AttributeDataType.OneOfMany, 12,
            "Self-assessed presentation level.");

        // A Markdown-formatted text attribute. It also gives the recruiter's CV search
        // something of the candidate's own to match on, since CV content is searched
        // through ProfileAttributeValues rather than through the CvRecords row.
        var summary = NewAttribute("Professional Summary", AttributeCategoryCatalog.DomainKnowledge, AttributeDataType.Text, 13,
            "Markdown-formatted summary shown at the top of a CV.");

        var beginner = new AttributeOption { Label = "Beginner", SortOrder = 0 };
        var intermediate = new AttributeOption { Label = "Intermediate", SortOrder = 1 };
        var advanced = new AttributeOption { Label = "Advanced", SortOrder = 2 };
        presentation.Options.Add(beginner);
        presentation.Options.Add(intermediate);
        presentation.Options.Add(advanced);

        db.AttributeDefinitions.AddRange(ielts, remoteWork, presentation, summary);

        var builtIns = await db.AttributeDefinitions
            .Where(a => a.IsBuiltIn)
            .ToDictionaryAsync(a => a.Name);

        // Spec §5.1: the "Me" attributes are the same engine as the library attributes.
        var firstName = builtIns["First Name"];
        var lastName = builtIns["Last Name"];
        var location = builtIns["Location"];

        var profile = new CandidateProfile { UserId = candidate.Id };
        db.CandidateProfiles.Add(profile);

        db.ProfileAttributeValues.AddRange(
            Value(profile, firstName, v => v.StringValue = "Sam"),
            Value(profile, lastName, v => v.StringValue = "Candidate"),
            Value(profile, location, v => v.StringValue = "Berlin"),
            Value(profile, ielts, v => v.NumericValue = 7.5m),
            Value(profile, remoteWork, v => v.BoolValue = true),
            Value(profile, presentation, v => v.SelectedOptionId = advanced.Id),
            Value(profile, summary, v => v.TextValue =
                "Backend engineer focused on **Python** and data engineering.\n\n" +
                "- Airflow and dbt for orchestration\n" +
                "- Blazor Server for internal tooling"));

        var analyticsPipeline = NewProject(
            "Sales Analytics Pipeline", 2025, 3,
            "Built an **ETL pipeline** ingesting 40M rows/day with `pandas` and Airflow.",
            ["Python", "Data Engineering"]);
        var churnModel = NewProject(
            "Churn Prediction Model", 2025, 6,
            "Gradient-boosted churn model, 0.91 AUC, deployed behind a REST endpoint.",
            ["Python", "Data Engineering"]);
        var recruiterDashboard = NewProject(
            "Recruiter Dashboard", 2025, 9,
            "Blazor Server dashboard with **live** SignalR updates for the hiring team.",
            ["Blazor", ".NET"]);

        db.Projects.AddRange(analyticsPipeline, churnModel, recruiterDashboard);
        db.CandidateProjects.AddRange(
            new CandidateProject { CandidateProfileId = profile.Id, ProjectId = analyticsPipeline.Id },
            new CandidateProject { CandidateProfileId = profile.Id, ProjectId = churnModel.Id },
            new CandidateProject { CandidateProfileId = profile.Id, ProjectId = recruiterDashboard.Id });

        var pythonPosition = new Position
        {
            Title = "Senior Python Engineer",
            Company = "Northwind Data",
            Description = "## About the role\n\nWe are hiring a **Senior Python Engineer** to own our data platform.\n\n- 5+ years of Python\n- ETL at scale\n\n> Remote-friendly, EU time zones.",
            IsPublic = true,
            IsOpen = true,
            MaxProjects = 3,
            CreatedByUserId = recruiter.Id,
            AttributeRules =
            [
                Rule(firstName, isRequired: true, 0),
                Rule(lastName, isRequired: true, 1),
                Rule(location, isRequired: false, 2),
                Rule(ielts, isRequired: true, 3),
                Rule(presentation, isRequired: false, 4),
                Rule(summary, isRequired: false, 5)
            ],
            // Both tags must match, so only the two Python/Data Engineering projects qualify.
            Tags = [new PositionTag { Tag = "Python" }, new PositionTag { Tag = "Data Engineering" }]
        };

        var frontendPosition = new Position
        {
            Title = "Blazor Frontend Developer",
            Company = "Northwind Data",
            Description = "## About the role\n\nJoin the team building our **Blazor Server** product.\n\n- C# and Razor components\n- CSS framework experience",
            IsPublic = true,
            IsOpen = true,
            MaxProjects = 2,
            CreatedByUserId = recruiter.Id,
            AttributeRules =
            [
                Rule(firstName, isRequired: true, 0),
                Rule(lastName, isRequired: true, 1),
                Rule(presentation, isRequired: true, 2),
                Rule(remoteWork, isRequired: false, 3)
            ],
            Tags = [new PositionTag { Tag = "Blazor" }]
        };

        // A Restricted position, gated by a numeric attribute filter (spec §7.2).
        var dataAnalystPosition = new Position
        {
            Title = "Data Analyst (IELTS > 6.5)",
            Company = "Northwind Data",
            Description = "## Restricted position\n\nVisible only to candidates whose **IELTS Score** is above 6.5.",
            IsPublic = false,
            IsOpen = true,
            MaxProjects = 2,
            CreatedByUserId = recruiter.Id,
            AttributeRules =
            [
                Rule(firstName, isRequired: true, 0),
                Rule(ielts, isRequired: true, 1)
            ],
            AccessRules = [new PositionAccessRule { AttributeDefinitionId = ielts.Id, Operator = PositionAccessOperator.GreaterThan, FilterValue = "6.5" }],
            Tags = [new PositionTag { Tag = "Python" }]
        };

        db.Positions.AddRange(pythonPosition, frontendPosition, dataAnalystPosition);

        var publishedCv = new CvRecord
        {
            Title = "My CV for Senior Python Engineer",
            CandidateProfileId = profile.Id,
            PositionId = pythonPosition.Id,
            Status = CvStatus.Published,
            CreatedByUserId = candidate.Id,
            LikeCount = 1
        };

        var draftCv = new CvRecord
        {
            Title = "My CV for Blazor Frontend Developer",
            CandidateProfileId = profile.Id,
            PositionId = frontendPosition.Id,
            Status = CvStatus.Draft,
            CreatedByUserId = candidate.Id
        };

        db.CvRecords.AddRange(publishedCv, draftCv);

        // Spec §9: only a Recruiter may like a CV.
        db.CvLikes.Add(new CvLike { CvRecordId = publishedCv.Id, UserId = recruiter.Id });

        db.DiscussionMessages.AddRange(
            new DiscussionMessage { PositionId = pythonPosition.Id, UserId = recruiter.Id, Content = "Welcome! Ask anything about the **data platform** team here.", CreatedAt = DateTimeOffset.UtcNow.AddDays(-2) },
            new DiscussionMessage { PositionId = pythonPosition.Id, UserId = candidate.Id, Content = "Is the ETL stack Airflow-based, or do you use something else?", CreatedAt = DateTimeOffset.UtcNow.AddDays(-1) },
            new DiscussionMessage { PositionId = pythonPosition.Id, UserId = recruiter.Id, Content = "Airflow + dbt. We can walk through it in the first call.", CreatedAt = DateTimeOffset.UtcNow.AddHours(-6) });

        await db.SaveChangesAsync();

        logger.LogInformation(
            "Demo data created.\n  Recruiter: {Recruiter} / {Password}\n  Candidate: {Candidate} / {Password}\n  Sample CVs, projects, positions and attributes are ready.",
            RecruiterEmail, DemoPassword, CandidateEmail, DemoPassword);
    }

    private static AttributeDefinition NewAttribute(
        string name,
        int categoryId,
        AttributeDataType dataType,
        int sortOrder,
        string description) =>
        new()
        {
            Name = name,
            Slug = AttributeSlug.FromName(name),
            CategoryId = categoryId,
            DataType = dataType,
            Description = description,
            SortOrder = sortOrder
        };

    private static PositionAttributeRule Rule(AttributeDefinition attribute, bool isRequired, int sortOrder) =>
        new()
        {
            AttributeDefinitionId = attribute.Id,
            IsRequired = isRequired,
            SortOrder = sortOrder,
            AttributeDefinition = attribute
        };

    private static Project NewProject(string name, int year, int month, string description, string[] tags) =>
        new()
        {
            Name = name,
            Description = description,
            StartDate = UtcDate.FromParts(year, month, 1),
            Url = "https://example.com/" + AttributeSlug.FromName(name),
            Tags = tags.Select(tag => new ProjectTag { Tag = tag }).ToList()
        };

    private static ProfileAttributeValue Value(
        CandidateProfile profile,
        AttributeDefinition attribute,
        Action<ProfileAttributeValue> assign)
    {
        var value = new ProfileAttributeValue
        {
            CandidateProfileId = profile.Id,
            AttributeDefinitionId = attribute.Id
        };
        assign(value);
        return value;
    }

    private static async Task<ApplicationUser?> EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string displayName,
        string role)
    {
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
            return existing;

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = displayName,
            EmailConfirmed = true,
            PreferredLanguage = "en",
            PreferredTheme = "light"
        };

        var created = await userManager.CreateAsync(user, DemoPassword);
        if (!created.Succeeded)
            return null;

        await userManager.AddToRoleAsync(user, role);
        return user;
    }
}

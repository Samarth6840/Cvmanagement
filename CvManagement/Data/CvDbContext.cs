using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using CvManagement.Data.Entities.Identity;

namespace CvManagement.Data;

public class CvDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    public CvDbContext(DbContextOptions<CvDbContext> options) : base(options) { }

    public DbSet<AttributeDefinition> AttributeDefinitions => Set<AttributeDefinition>();
    public DbSet<AttributeOption> AttributeOptions => Set<AttributeOption>();
    public DbSet<CandidateProfile> CandidateProfiles => Set<CandidateProfile>();
    public DbSet<ProfileAttributeValue> ProfileAttributeValues => Set<ProfileAttributeValue>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<PositionAttributeRule> PositionAttributeRules => Set<PositionAttributeRule>();
    public DbSet<PositionAccessRule> PositionAccessRules => Set<PositionAccessRule>();
    public DbSet<PositionTag> PositionTags => Set<PositionTag>();
    public DbSet<CvRecord> CvRecords => Set<CvRecord>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectTag> ProjectTags => Set<ProjectTag>();
    public DbSet<CandidateProject> CandidateProjects => Set<CandidateProject>();
    public DbSet<CvLike> CvLikes => Set<CvLike>();
    public DbSet<DiscussionMessage> DiscussionMessages => Set<DiscussionMessage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(CvDbContext).Assembly);

        builder.Entity<ApplicationUser>(e => e.ToTable("Users"));
        builder.Entity<ApplicationRole>(e => e.ToTable("Roles"));
        builder.Entity<IdentityUserRole<Guid>>(e => e.ToTable("UserRoles"));
        builder.Entity<IdentityUserClaim<Guid>>(e => e.ToTable("UserClaims"));
        builder.Entity<IdentityUserLogin<Guid>>(e => e.ToTable("UserLogins"));
        builder.Entity<IdentityUserToken<Guid>>(e => e.ToTable("UserTokens"));
        builder.Entity<IdentityRoleClaim<Guid>>(e => e.ToTable("RoleClaims"));
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditFields();
        return await base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyAuditFields();
        return base.SaveChanges();
    }

    private void ApplyAuditFields()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Modified)
                continue;

            if (entry.Properties.Any(p => p.Metadata.Name == nameof(IAuditable.UpdatedAt)))
            {
                entry.Property(nameof(IAuditable.UpdatedAt)).CurrentValue = now;
            }
        }
    }
}

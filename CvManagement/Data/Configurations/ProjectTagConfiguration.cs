using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CvManagement.Data.Entities.Projects;

namespace CvManagement.Data.Configurations;

public class ProjectTagConfiguration : IEntityTypeConfiguration<ProjectTag>
{
    public void Configure(EntityTypeBuilder<ProjectTag> b)
    {
        b.HasKey(e => e.Id);
        b.HasIndex(e => new { e.ProjectId, e.Tag }).IsUnique();

        b.HasOne(e => e.Project)
            .WithMany(p => p.Tags)
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

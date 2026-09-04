using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CvManagement.Data.Entities.Projects;

namespace CvManagement.Data.Configurations;

public class CandidateProjectConfiguration : IEntityTypeConfiguration<CandidateProject>
{
    public void Configure(EntityTypeBuilder<CandidateProject> b)
    {
        b.HasKey(e => e.Id);
        b.HasIndex(e => new { e.CandidateProfileId, e.ProjectId }).IsUnique();

        b.HasOne(e => e.CandidateProfile)
            .WithMany(p => p.CandidateProjects)
            .HasForeignKey(e => e.CandidateProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(e => e.Project)
            .WithMany(p => p.CandidateProjects)
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

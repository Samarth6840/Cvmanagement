using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CvManagement.Data.Entities.Profiles;

namespace CvManagement.Data.Configurations;

public class CandidateProfileConfiguration : IEntityTypeConfiguration<CandidateProfile>
{
    public void Configure(EntityTypeBuilder<CandidateProfile> b)
    {
        b.HasKey(e => e.Id);
        b.HasIndex(e => e.UserId).IsUnique();
        b.Property(e => e.RowVersion).IsRowVersion().IsRequired(false);

        b.HasOne(e => e.User)
            .WithOne(u => u.CandidateProfile)
            .HasForeignKey<CandidateProfile>(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

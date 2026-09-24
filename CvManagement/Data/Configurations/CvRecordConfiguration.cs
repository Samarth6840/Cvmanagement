using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CvManagement.Data.Entities.Cv;

namespace CvManagement.Data.Configurations;

public class CvRecordConfiguration : IEntityTypeConfiguration<CvRecord>
{
    public void Configure(EntityTypeBuilder<CvRecord> b)
    {
        b.HasKey(e => e.Id);
        b.HasIndex(e => e.CandidateProfileId);
        b.HasIndex(e => e.PositionId);
        b.HasIndex(e => e.Status);
        b.HasIndex(e => e.CreatedByUserId);
        b.Property(e => e.RowVersion).IsRowVersion();

        b.Property(e => e.SearchVector);
        b.HasIndex("SearchVector").HasMethod("gin");

        b.HasOne(e => e.CandidateProfile)
            .WithMany()
            .HasForeignKey(e => e.CandidateProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(e => e.Position)
            .WithMany(p => p.CvRecords)
            .HasForeignKey(e => e.PositionId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(e => e.CreatedByUser)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(e => e.Likes)
            .WithOne(l => l.CvRecord)
            .HasForeignKey(l => l.CvRecordId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

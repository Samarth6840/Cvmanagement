using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CvManagement.Data.Entities.Profiles;

namespace CvManagement.Data.Configurations;

public class ProfileAttributeValueConfiguration : IEntityTypeConfiguration<ProfileAttributeValue>
{
    public void Configure(EntityTypeBuilder<ProfileAttributeValue> b)
    {
        b.HasKey(e => e.Id);
        b.HasIndex(e => new { e.CandidateProfileId, e.AttributeDefinitionId }).IsUnique();
        b.Property(e => e.StringValue).HasMaxLength(512);
        b.Property(e => e.ImageUrl).HasMaxLength(512);

        b.HasOne(e => e.CandidateProfile)
            .WithMany(p => p.AttributeValues)
            .HasForeignKey(e => e.CandidateProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(e => e.AttributeDefinition)
            .WithMany(a => a.ProfileValues)
            .HasForeignKey(e => e.AttributeDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(e => e.SelectedOption)
            .WithMany()
            .HasForeignKey(e => e.SelectedOptionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CvManagement.Data.Entities.Positions;

namespace CvManagement.Data.Configurations;

public class PositionTagConfiguration : IEntityTypeConfiguration<PositionTag>
{
    public void Configure(EntityTypeBuilder<PositionTag> b)
    {
        b.HasKey(e => e.Id);
        b.HasIndex(e => new { e.PositionId, e.Tag }).IsUnique();

        b.HasOne(e => e.Position)
            .WithMany(p => p.Tags)
            .HasForeignKey(e => e.PositionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

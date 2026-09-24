using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CvManagement.Data.Entities.Positions;

namespace CvManagement.Data.Configurations;

public class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> b)
    {
        b.HasKey(e => e.Id);
        b.HasIndex(e => e.Title);
        b.HasIndex(e => e.IsPublic);
        b.HasIndex(e => e.CreatedAt);
        b.HasIndex(e => e.CreatedByUserId);
        b.Property(e => e.RowVersion).IsRowVersion().IsRequired(false);

        b.Property(e => e.SearchVector);
        b.HasIndex("SearchVector").HasMethod("gin");

        b.HasMany(e => e.AttributeRules)
            .WithOne(r => r.Position)
            .HasForeignKey(r => r.PositionId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(e => e.Tags)
            .WithOne(t => t.Position)
            .HasForeignKey(t => t.PositionId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(e => e.AccessRules)
            .WithOne(a => a.Position)
            .HasForeignKey(a => a.PositionId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(e => e.CvRecords)
            .WithOne(c => c.Position)
            .HasForeignKey(c => c.PositionId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(e => e.DiscussionMessages)
            .WithOne(d => d.Position)
            .HasForeignKey(d => d.PositionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

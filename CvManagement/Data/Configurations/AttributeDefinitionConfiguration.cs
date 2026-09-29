using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CvManagement.Data.Entities.Attributes;

namespace CvManagement.Data.Configurations;

public class AttributeDefinitionConfiguration : IEntityTypeConfiguration<AttributeDefinition>
{
    public void Configure(EntityTypeBuilder<AttributeDefinition> b)
    {
        b.HasKey(e => e.Id);
        b.HasIndex(e => e.IsBuiltIn);
        b.HasIndex(e => e.CategoryId);
        b.HasIndex(e => e.Slug).IsUnique();
        b.HasIndex(e => e.Name).IsUnique();
        b.Property(e => e.Name).HasMaxLength(128);
        b.Property(e => e.Slug).HasMaxLength(128);
        b.Property(e => e.Description).HasMaxLength(1024);
        b.Property(e => e.RowVersion).IsRowVersion().IsRequired(false);

        b.HasMany(e => e.Options)
            .WithOne(o => o.AttributeDefinition)
            .HasForeignKey(o => o.AttributeDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict, not cascade: deleting a category must not take its attributes with it.
        // There is no category admin UI, so this only guards direct SQL.
        b.HasOne(e => e.Category)
            .WithMany(c => c.Attributes)
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

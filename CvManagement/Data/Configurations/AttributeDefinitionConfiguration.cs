using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CvManagement.Data.Entities.Attributes;

namespace CvManagement.Data.Configurations;

public class AttributeDefinitionConfiguration : IEntityTypeConfiguration<AttributeDefinition>
{
    public void Configure(EntityTypeBuilder<AttributeDefinition> b)
    {
        b.HasKey(e => e.Id);
        b.HasIndex(e => e.Slug).IsUnique();
        b.Property(e => e.Name).HasMaxLength(128);
        b.Property(e => e.Slug).HasMaxLength(128);
        b.Property(e => e.Description).HasMaxLength(1024);
        b.Property(e => e.RowVersion).IsRowVersion();

        b.HasMany(e => e.Options)
            .WithOne(o => o.AttributeDefinition)
            .HasForeignKey(o => o.AttributeDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

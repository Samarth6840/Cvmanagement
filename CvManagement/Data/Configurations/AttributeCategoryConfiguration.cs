using CvManagement.Data.Entities.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CvManagement.Data.Configurations;

public class AttributeCategoryConfiguration : IEntityTypeConfiguration<AttributeCategory>
{
    public void Configure(EntityTypeBuilder<AttributeCategory> b)
    {
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.Property(e => e.Name).HasMaxLength(64).IsRequired();
        b.HasIndex(e => e.Name).IsUnique();
    }
}

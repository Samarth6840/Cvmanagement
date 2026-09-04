using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CvManagement.Data.Entities.Identity;

namespace CvManagement.Data.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> b)
    {
        b.HasKey(e => e.Id);
        b.HasIndex(e => e.Email).IsUnique();
        b.HasIndex(e => e.UserName).IsUnique();
        b.Property(e => e.DisplayName).HasMaxLength(128);
        b.Property(e => e.PreferredLanguage).HasMaxLength(10);
        b.Property(e => e.PreferredTheme).HasMaxLength(10);
        b.Property(e => e.RowVersion).IsRowVersion();
    }
}

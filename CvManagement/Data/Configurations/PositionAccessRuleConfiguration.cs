using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CvManagement.Data.Entities.Positions;

namespace CvManagement.Data.Configurations;

public class PositionAccessRuleConfiguration : IEntityTypeConfiguration<PositionAccessRule>
{
    public void Configure(EntityTypeBuilder<PositionAccessRule> b)
    {
        b.HasKey(e => e.Id);

        b.HasOne(e => e.Position)
            .WithMany(p => p.AccessRules)
            .HasForeignKey(e => e.PositionId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(e => e.AttributeDefinition)
            .WithMany(a => a.PositionAccessRules)
            .HasForeignKey(e => e.AttributeDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

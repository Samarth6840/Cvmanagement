using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CvManagement.Data.Entities.Positions;

namespace CvManagement.Data.Configurations;

public class PositionAttributeRuleConfiguration : IEntityTypeConfiguration<PositionAttributeRule>
{
    public void Configure(EntityTypeBuilder<PositionAttributeRule> b)
    {
        b.HasKey(e => e.Id);
        b.HasIndex(e => new { e.PositionId, e.AttributeDefinitionId }).IsUnique();

        b.HasOne(e => e.Position)
            .WithMany(p => p.AttributeRules)
            .HasForeignKey(e => e.PositionId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(e => e.AttributeDefinition)
            .WithMany(a => a.PositionAttributeRules)
            .HasForeignKey(e => e.AttributeDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

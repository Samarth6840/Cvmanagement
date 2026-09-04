using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CvManagement.Data.Entities.Discussions;

namespace CvManagement.Data.Configurations;

public class DiscussionMessageConfiguration : IEntityTypeConfiguration<DiscussionMessage>
{
    public void Configure(EntityTypeBuilder<DiscussionMessage> b)
    {
        b.HasKey(e => e.Id);
        b.HasIndex(e => new { e.PositionId, e.CreatedAt });

        b.HasOne(e => e.Position)
            .WithMany(p => p.DiscussionMessages)
            .HasForeignKey(e => e.PositionId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(e => e.User)
            .WithMany(u => u.DiscussionMessages)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

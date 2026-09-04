using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CvManagement.Data.Entities.Likes;

namespace CvManagement.Data.Configurations;

public class CvLikeConfiguration : IEntityTypeConfiguration<CvLike>
{
    public void Configure(EntityTypeBuilder<CvLike> b)
    {
        b.HasKey(e => e.Id);
        b.HasIndex(e => new { e.CvRecordId, e.UserId }).IsUnique();

        b.HasOne(e => e.CvRecord)
            .WithMany(c => c.Likes)
            .HasForeignKey(e => e.CvRecordId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(e => e.User)
            .WithMany(u => u.GivenLikes)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

namespace CvManagement.Data.Entities.Likes;

public class CvLike
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CvRecordId { get; set; }

    public Guid UserId { get; set; }

    public DateTimeOffset LikedAt { get; set; } = DateTimeOffset.UtcNow;

    public CvRecord CvRecord { get; set; } = null!;

    public ApplicationUser User { get; set; } = null!;
}

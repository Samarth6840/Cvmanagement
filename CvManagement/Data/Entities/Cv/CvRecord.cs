using System.ComponentModel.DataAnnotations;

namespace CvManagement.Data.Entities.Cv;

public class CvRecord : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(256)]
    public string Title { get; set; } = string.Empty;

    public Guid CandidateProfileId { get; set; }

    public Guid PositionId { get; set; }

    public CvStatus Status { get; set; } = CvStatus.Draft;

    public int LikeCount { get; set; }

    public Guid CreatedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];

    public CandidateProfile CandidateProfile { get; set; } = null!;

    public Position Position { get; set; } = null!;

    public ApplicationUser CreatedByUser { get; set; } = null!;

    public ICollection<CvLike> Likes { get; set; } = [];
}

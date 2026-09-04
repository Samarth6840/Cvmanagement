using System.ComponentModel.DataAnnotations;

namespace CvManagement.Data.Entities.Profiles;

public class CandidateProfile : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];

    public ApplicationUser User { get; set; } = null!;

    public ICollection<ProfileAttributeValue> AttributeValues { get; set; } = [];

    public ICollection<CandidateProject> CandidateProjects { get; set; } = [];
}

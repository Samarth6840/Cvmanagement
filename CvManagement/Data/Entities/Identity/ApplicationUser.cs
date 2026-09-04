using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace CvManagement.Data.Entities.Identity;

public class ApplicationUser : IdentityUser<Guid>, IAuditable
{
    [MaxLength(128)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(512)]
    public string? AvatarUrl { get; set; }

    [MaxLength(10)]
    public string PreferredLanguage { get; set; } = "en";

    [MaxLength(10)]
    public string PreferredTheme { get; set; } = "light";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];

    public CandidateProfile? CandidateProfile { get; set; }

    public ICollection<CvLike> GivenLikes { get; set; } = [];

    public ICollection<DiscussionMessage> DiscussionMessages { get; set; } = [];
}

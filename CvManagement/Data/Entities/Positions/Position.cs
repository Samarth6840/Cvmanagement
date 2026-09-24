using System.ComponentModel.DataAnnotations;
using NpgsqlTypes;

namespace CvManagement.Data.Entities.Positions;

public class Position : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(256)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [MaxLength(256)]
    public string? Company { get; set; }

    public bool IsPublic { get; set; } = true;

    public bool IsOpen { get; set; } = true;

    public int MaxProjects { get; set; } = 5;

    public Guid CreatedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];

    public NpgsqlTsVector SearchVector { get; set; } = NpgsqlTsVector.Empty;

    public ApplicationUser CreatedByUser { get; set; } = null!;

    public ICollection<PositionAttributeRule> AttributeRules { get; set; } = [];

    public ICollection<PositionTag> Tags { get; set; } = [];

    public ICollection<PositionAccessRule> AccessRules { get; set; } = [];

    public ICollection<CvRecord> CvRecords { get; set; } = [];

    public ICollection<DiscussionMessage> DiscussionMessages { get; set; } = [];
}

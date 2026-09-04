using System.ComponentModel.DataAnnotations;

namespace CvManagement.Data.Entities.Attributes;

public class AttributeDefinition : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(128)]
    public string Slug { get; set; } = string.Empty;

    public AttributeCategory Category { get; set; }

    public AttributeDataType DataType { get; set; }

    [MaxLength(1024)]
    public string? Description { get; set; }

    public bool IsRequired { get; set; }

    public int SortOrder { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];

    public ICollection<AttributeOption> Options { get; set; } = [];

    public ICollection<ProfileAttributeValue> ProfileValues { get; set; } = [];

    public ICollection<PositionAttributeRule> PositionAttributeRules { get; set; } = [];

    public ICollection<PositionAccessRule> PositionAccessRules { get; set; } = [];
}

using System.ComponentModel.DataAnnotations;

namespace CvManagement.Data.Entities.Positions;

public class PositionAttributeRule
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PositionId { get; set; }

    public Guid AttributeDefinitionId { get; set; }

    public bool IsRequired { get; set; }

    public int SortOrder { get; set; }

    public Position Position { get; set; } = null!;

    public AttributeDefinition AttributeDefinition { get; set; } = null!;
}

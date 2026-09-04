using System.ComponentModel.DataAnnotations;

namespace CvManagement.Data.Entities.Positions;

public class PositionAccessRule
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PositionId { get; set; }

    public Guid AttributeDefinitionId { get; set; }

    public PositionAccessOperator Operator { get; set; }

    [MaxLength(512)]
    public string FilterValue { get; set; } = string.Empty;

    public Position Position { get; set; } = null!;

    public AttributeDefinition AttributeDefinition { get; set; } = null!;
}

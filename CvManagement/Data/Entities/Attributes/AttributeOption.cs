using System.ComponentModel.DataAnnotations;

namespace CvManagement.Data.Entities.Attributes;

public class AttributeOption
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AttributeDefinitionId { get; set; }

    [MaxLength(128)]
    public string Label { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public AttributeDefinition AttributeDefinition { get; set; } = null!;
}

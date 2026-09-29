using System.ComponentModel.DataAnnotations;

namespace CvManagement.Data.Entities.Attributes;

public class AttributeCategory
{
    public int Id { get; set; }

    [MaxLength(64)]
    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public ICollection<AttributeDefinition> Attributes { get; set; } = [];
}

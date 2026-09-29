using System.ComponentModel.DataAnnotations;

namespace CvManagement.Data.Entities.Attributes;

/// <summary>
/// The shared attribute category lookup (spec §13.1). A category is purely a UI grouping and
/// filtering aid: no business logic may depend on it, and a Recruiter can attach any attribute
/// to any position regardless of category. There is no admin UI for managing categories — the
/// list is fixed and seeded, but the table can be extended at the database level.
/// </summary>
public class AttributeCategory
{
    /// <summary>Assigned from <see cref="AttributeCategoryCatalog"/>, not by the database.</summary>
    public int Id { get; set; }

    [MaxLength(64)]
    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public ICollection<AttributeDefinition> Attributes { get; set; } = [];
}

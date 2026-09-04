using System.ComponentModel.DataAnnotations;

namespace CvManagement.Data.Entities.Projects;

public class ProjectTag
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }

    [MaxLength(64)]
    public string Tag { get; set; } = string.Empty;

    public Project Project { get; set; } = null!;
}

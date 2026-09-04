using System.ComponentModel.DataAnnotations;

namespace CvManagement.Data.Entities.Projects;

public class Project : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(256)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [MaxLength(512)]
    public string? Url { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];

    public ICollection<ProjectTag> Tags { get; set; } = [];

    public ICollection<CandidateProject> CandidateProjects { get; set; } = [];
}

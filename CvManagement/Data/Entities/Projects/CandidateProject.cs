using System.ComponentModel.DataAnnotations;

namespace CvManagement.Data.Entities.Projects;

public class CandidateProject
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CandidateProfileId { get; set; }

    public Guid ProjectId { get; set; }

    [MaxLength(128)]
    public string? Role { get; set; }

    public string? Contribution { get; set; }

    public CandidateProfile CandidateProfile { get; set; } = null!;

    public Project Project { get; set; } = null!;
}

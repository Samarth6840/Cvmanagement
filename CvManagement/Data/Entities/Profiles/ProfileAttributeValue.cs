using System.ComponentModel.DataAnnotations;

namespace CvManagement.Data.Entities.Profiles;

public class ProfileAttributeValue
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CandidateProfileId { get; set; }

    public Guid AttributeDefinitionId { get; set; }

    [MaxLength(512)]
    public string? StringValue { get; set; }

    public string? TextValue { get; set; }

    [MaxLength(512)]
    public string? ImageUrl { get; set; }

    public decimal? NumericValue { get; set; }

    public DateTime? DateValue { get; set; }

    public DateTime? PeriodStart { get; set; }

    public DateTime? PeriodEnd { get; set; }

    public bool? BoolValue { get; set; }

    public Guid? SelectedOptionId { get; set; }

    public CandidateProfile CandidateProfile { get; set; } = null!;

    public AttributeDefinition AttributeDefinition { get; set; } = null!;

    public AttributeOption? SelectedOption { get; set; }
}

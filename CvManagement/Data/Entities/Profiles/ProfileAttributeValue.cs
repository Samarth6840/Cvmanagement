using System.ComponentModel.DataAnnotations;
using NpgsqlTypes;

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

    // CV content lives here (EAV), so this is what recruiters' full-text search
    // actually matches. Maintained by a DB trigger, not by the CvRecords row trigger,
    // which cannot see attribute values at all.
    public NpgsqlTsVector SearchVector { get; set; } = NpgsqlTsVector.Empty;

    public CandidateProfile CandidateProfile { get; set; } = null!;

    public AttributeDefinition AttributeDefinition { get; set; } = null!;

    public AttributeOption? SelectedOption { get; set; }
}

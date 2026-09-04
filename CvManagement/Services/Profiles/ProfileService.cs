using CvManagement.Data;
using CvManagement.Data.Entities.Profiles;
using CvManagement.Data.Entities.Attributes;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Services.Profiles;

public interface IProfileService
{
    Task<CandidateProfile> GetOrCreateProfileAsync(Guid userId);
    Task<CandidateProfile?> GetProfileAsync(Guid profileId);
    Task<ProfileAttributeValue?> UpsertAttributeValueAsync(Guid profileId, Guid attributeId, string? stringValue = null, decimal? numericValue = null, DateTime? dateValue = null, DateTime? periodStart = null, DateTime? periodEnd = null, bool? boolValue = null, Guid? selectedOptionId = null, string? imageUrl = null, string? textValue = null);
}

public class ProfileService : IProfileService
{
    private readonly CvDbContext _db;

    public ProfileService(CvDbContext db) => _db = db;

    public async Task<CandidateProfile> GetOrCreateProfileAsync(Guid userId)
    {
        var existing = await _db.CandidateProfiles
            .Include(p => p.AttributeValues)
                .ThenInclude(v => v.AttributeDefinition)
            .Include(p => p.AttributeValues)
                .ThenInclude(v => v.SelectedOption)
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (existing is not null) return existing;

        var profile = new CandidateProfile
        {
            UserId = userId
        };
        _db.CandidateProfiles.Add(profile);
        await _db.SaveChangesAsync();
        return profile;
    }

    public async Task<CandidateProfile?> GetProfileAsync(Guid profileId) =>
        await _db.CandidateProfiles
            .Include(p => p.User)
            .Include(p => p.AttributeValues)
                .ThenInclude(v => v.AttributeDefinition)
            .Include(p => p.AttributeValues)
                .ThenInclude(v => v.SelectedOption)
            .FirstOrDefaultAsync(p => p.Id == profileId);

    public async Task<ProfileAttributeValue?> UpsertAttributeValueAsync(
        Guid profileId,
        Guid attributeId,
        string? stringValue = null,
        decimal? numericValue = null,
        DateTime? dateValue = null,
        DateTime? periodStart = null,
        DateTime? periodEnd = null,
        bool? boolValue = null,
        Guid? selectedOptionId = null,
        string? imageUrl = null,
        string? textValue = null)
    {
        var existing = await _db.ProfileAttributeValues
            .FirstOrDefaultAsync(v => v.CandidateProfileId == profileId && v.AttributeDefinitionId == attributeId);

        if (existing is null)
        {
            existing = new ProfileAttributeValue
            {
                CandidateProfileId = profileId,
                AttributeDefinitionId = attributeId
            };
            _db.ProfileAttributeValues.Add(existing);
        }

        existing.StringValue = stringValue;
        existing.TextValue = textValue;
        existing.ImageUrl = imageUrl;
        existing.NumericValue = numericValue;
        existing.DateValue = dateValue;
        existing.PeriodStart = periodStart;
        existing.PeriodEnd = periodEnd;
        existing.BoolValue = boolValue;
        existing.SelectedOptionId = selectedOptionId;

        await _db.SaveChangesAsync();
        return existing;
    }
}

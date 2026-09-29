using CvManagement.Data;
using CvManagement.Data.Entities.Attributes;
using CvManagement.Data.Entities.Profiles;
using CvManagement.Services.Concurrency;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Services.Profiles;

public record AttributeValueInput(
    Guid AttributeDefinitionId,
    string? StringValue = null,
    string? TextValue = null,
    string? ImageUrl = null,
    decimal? NumericValue = null,
    DateTime? DateValue = null,
    DateTime? PeriodStart = null,
    DateTime? PeriodEnd = null,
    bool? BoolValue = null,
    Guid? SelectedOptionId = null);

public interface IProfileService
{
    Task<CandidateProfile> GetOrCreateProfileAsync(Guid userId);
    Task<CandidateProfile?> GetByUserIdAsync(Guid userId);

    Task<byte[]> SaveValuesAsync(
        Guid profileId,
        IReadOnlyCollection<AttributeValueInput> values,
        byte[] expectedVersion);
}

public class ProfileService : IProfileService
{
    private const string EntityName = "Your profile";

    private readonly CvDbContext _db;

    public ProfileService(CvDbContext db) => _db = db;

    public Task<CandidateProfile?> GetByUserIdAsync(Guid userId) =>
        LoadProfileAsync(p => p.UserId == userId);

    public async Task<CandidateProfile> GetOrCreateProfileAsync(Guid userId)
    {
        var existing = await LoadProfileAsync(p => p.UserId == userId);
        if (existing is not null) return existing;

        var profile = new CandidateProfile { UserId = userId };
        _db.CandidateProfiles.Add(profile);
        await _db.SaveChangesAsync();
        return profile;
    }

    private Task<CandidateProfile?> LoadProfileAsync(System.Linq.Expressions.Expression<Func<CandidateProfile, bool>> predicate) =>
        _db.CandidateProfiles
            .AsNoTracking()
            .Include(p => p.AttributeValues).ThenInclude(v => v.AttributeDefinition)
            .Include(p => p.AttributeValues).ThenInclude(v => v.SelectedOption)
            .FirstOrDefaultAsync(predicate);

    public async Task<byte[]> SaveValuesAsync(
        Guid profileId,
        IReadOnlyCollection<AttributeValueInput> values,
        byte[] expectedVersion)
    {
        _db.ChangeTracker.Clear();

        var profile = await _db.CandidateProfiles
            .Include(p => p.AttributeValues)
            .FirstOrDefaultAsync(p => p.Id == profileId)
            ?? throw new InvalidOperationException("Profile not found");

        if (!VersionsMatch(profile.RowVersion, expectedVersion))
            throw new ConcurrencyConflictException(EntityName);

        var existingByAttribute = profile.AttributeValues.ToDictionary(v => v.AttributeDefinitionId);
        foreach (var input in values)
            ApplyInput(_db, existingByAttribute, profile.Id, input);

        profile.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException(EntityName);
        }

        return profile.RowVersion;
    }

    private static bool VersionsMatch(byte[]? stored, byte[] expected) =>
        stored is not null && expected is not null && stored.AsSpan().SequenceEqual(expected);

    private static void ApplyInput(
        CvDbContext db,
        Dictionary<Guid, ProfileAttributeValue> existingByAttribute,
        Guid profileId,
        AttributeValueInput input)
    {
        if (!existingByAttribute.TryGetValue(input.AttributeDefinitionId, out var existing))
        {
            existing = new ProfileAttributeValue
            {
                Id = Guid.NewGuid(),
                CandidateProfileId = profileId,
                AttributeDefinitionId = input.AttributeDefinitionId
            };
            existingByAttribute[input.AttributeDefinitionId] = existing;
            db.ProfileAttributeValues.Add(existing);
        }

        existing.StringValue = input.StringValue;
        existing.TextValue = input.TextValue;
        existing.ImageUrl = input.ImageUrl;
        existing.NumericValue = input.NumericValue;
        existing.DateValue = input.DateValue;
        existing.PeriodStart = input.PeriodStart;
        existing.PeriodEnd = input.PeriodEnd;
        existing.BoolValue = input.BoolValue;
        existing.SelectedOptionId = input.SelectedOptionId;
    }
}

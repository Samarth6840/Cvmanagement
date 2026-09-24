using CvManagement.Data.Entities.Attributes;
using CvManagement.Data.Entities.Cv;
using CvManagement.Data.Entities.Profiles;
using CvManagement.Data.Entities.Positions;
using CvManagement.Data.Entities.Projects;
using CvManagement.Services.Positions;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Services.Cv;

public interface ICvService
{
    Task<List<CvRecord>> GetCandidateCvsAsync(Guid candidateProfileId);
    Task<List<CvRecord>> GetPublishedCvsForPositionAsync(Guid positionId);
    Task<CvRecord?> GetByIdAsync(Guid id);
    Task<CvRecord> CreateAsync(Guid candidateProfileId, Guid positionId, string title, Guid createdByUserId);
    Task DeleteAsync(Guid id);
    Task<CvRecord> PublishAsync(Guid id);
    Task<CvRecord> UnpublishAsync(Guid id);
    Task UpdateTitleAsync(Guid id, string title);
    Task<List<ProfileAttributeValue>> GetCvAttributeValuesAsync(Guid cvId);
    Task<List<AttributeDefinition>> GetCvAttributeDefinitionsAsync(Guid cvId);
    Task<ProfileAttributeValue?> GetCvAttributeValueAsync(Guid cvId, Guid attributeDefinitionId);
    Task SaveAttributeValueAsync(Guid cvId, ProfileAttributeValue value);
    Task<List<AttributeDefinition>> GetMissingRequiredAttributesAsync(Guid cvId);
    Task<List<Project>> GetCvProjectsAsync(Guid cvId);
    bool IsValueFilled(ProfileAttributeValue? value);
    Task<List<CvRecord>> GetVisibleCvsForPositionAsync(Guid positionId);
}

public class CvService : ICvService
{
    private readonly Data.CvDbContext _db;

    public CvService(Data.CvDbContext db) => _db = db;

    public async Task<List<CvRecord>> GetCandidateCvsAsync(Guid candidateProfileId)
    {
        var cvs = await _db.CvRecords
            .Include(c => c.Position)
            .Include(c => c.Likes)
            .Include(c => c.Position!.AccessRules).ThenInclude(r => r.AttributeDefinition)
            .Where(c => c.CandidateProfileId == candidateProfileId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        // Hide CVs for positions the candidate can no longer access (§7.4) — including from the owner's view.
        var values = await _db.ProfileAttributeValues
            .Include(v => v.SelectedOption)
            .Where(v => v.CandidateProfileId == candidateProfileId)
            .ToListAsync();
        return cvs
            .Where(c => c.Position is not null && AccessRuleEvaluator.CanAccess(c.Position.AccessRules, values))
            .ToList();
    }

    public async Task<List<CvRecord>> GetPublishedCvsForPositionAsync(Guid positionId) =>
        await _db.CvRecords
            .Include(c => c.CandidateProfile).ThenInclude(p => p!.User)
            .Include(c => c.Likes)
            .Where(c => c.PositionId == positionId && c.Status == CvStatus.Published)
            .OrderByDescending(c => c.LikeCount)
            .ToListAsync();

    public async Task<List<CvRecord>> GetVisibleCvsForPositionAsync(Guid positionId)
    {
        var cvs = await GetPublishedCvsForPositionAsync(positionId);
        // ponytail: per-CV evaluation in memory; acceptable for demo scale
        var visible = new List<CvRecord>();
        foreach (var cv in cvs)
        {
            if (cv.CandidateProfileId == Guid.Empty) continue;
            if (await CanCandidateAccessCvAsync(cv))
                visible.Add(cv);
        }
        return visible;
    }

    private async Task<bool> CanCandidateAccessCvAsync(CvRecord cv)
    {
        var rules = await _db.PositionAccessRules
            .Include(r => r.AttributeDefinition)
            .Where(r => r.PositionId == cv.PositionId)
            .ToListAsync();
        if (rules.Count == 0) return true;
        var values = await _db.ProfileAttributeValues
            .Include(v => v.SelectedOption)
            .Where(v => v.CandidateProfileId == cv.CandidateProfileId)
            .ToListAsync();
        return AccessRuleEvaluator.CanAccess(rules, values);
    }

    public async Task<CvRecord?> GetByIdAsync(Guid id) =>
        await _db.CvRecords
            .Include(c => c.CandidateProfile).ThenInclude(p => p!.User)
            .Include(c => c.Position).ThenInclude(p => p!.AttributeRules).ThenInclude(r => r.AttributeDefinition)
            .Include(c => c.Position).ThenInclude(p => p!.Tags)
            .Include(c => c.Likes)
            .FirstOrDefaultAsync(c => c.Id == id);

    public async Task<CvRecord> CreateAsync(Guid candidateProfileId, Guid positionId, string title, Guid createdByUserId)
    {
        var exists = await _db.CvRecords.AnyAsync(c => c.CandidateProfileId == candidateProfileId && c.PositionId == positionId);
        if (exists)
            throw new InvalidOperationException("A CV for this position already exists.");

        var cv = new CvRecord
        {
            Title = title,
            CandidateProfileId = candidateProfileId,
            PositionId = positionId,
            Status = CvStatus.Draft,
            CreatedByUserId = createdByUserId
        };
        _db.CvRecords.Add(cv);
        await _db.SaveChangesAsync();
        return cv;
    }

    public async Task DeleteAsync(Guid id)
    {
        var cv = await _db.CvRecords.FindAsync(id)
            ?? throw new InvalidOperationException("CV not found");
        _db.CvRecords.Remove(cv);
        await _db.SaveChangesAsync();
    }

    public async Task<CvRecord> PublishAsync(Guid id)
    {
        var cv = await _db.CvRecords
            .Include(c => c.CandidateProfile)
            .FirstOrDefaultAsync(c => c.Id == id) ?? throw new InvalidOperationException("CV not found");

        var missing = await GetMissingRequiredAttributesAsync(id);
        if (missing.Any())
            throw new InvalidOperationException("Not all required attributes are filled.");

        cv.Status = CvStatus.Published;
        await _db.SaveChangesAsync();
        return cv;
    }

    public async Task<CvRecord> UnpublishAsync(Guid id)
    {
        var cv = await _db.CvRecords.FindAsync(id)
            ?? throw new InvalidOperationException("CV not found");
        cv.Status = CvStatus.Draft;
        await _db.SaveChangesAsync();
        return cv;
    }

    public async Task UpdateTitleAsync(Guid id, string title)
    {
        var cv = await _db.CvRecords.FindAsync(id)
            ?? throw new InvalidOperationException("CV not found");
        cv.Title = title;
        await _db.SaveChangesAsync();
    }

    public async Task<List<ProfileAttributeValue>> GetCvAttributeValuesAsync(Guid cvId)
    {
        var cv = await _db.CvRecords
            .Include(c => c.CandidateProfile).ThenInclude(p => p!.AttributeValues)
                .ThenInclude(v => v.AttributeDefinition)
            .Include(c => c.Position).ThenInclude(p => p!.AttributeRules)
            .FirstOrDefaultAsync(c => c.Id == cvId);

        if (cv?.CandidateProfile is null || cv.Position is null) return new();

        var positionAttrIds = cv.Position.AttributeRules.Select(r => r.AttributeDefinitionId).ToHashSet();
        return cv.CandidateProfile.AttributeValues
            .Where(v => positionAttrIds.Contains(v.AttributeDefinitionId))
            .ToList();
    }

    public async Task<List<AttributeDefinition>> GetCvAttributeDefinitionsAsync(Guid cvId)
    {
        var cv = await _db.CvRecords
            .Include(c => c.Position).ThenInclude(p => p!.AttributeRules)
                .ThenInclude(r => r.AttributeDefinition).ThenInclude(d => d.Options)
            .FirstOrDefaultAsync(c => c.Id == cvId);
        return cv?.Position?.AttributeRules
            .OrderBy(r => r.SortOrder)
            .Select(r => r.AttributeDefinition)
            .ToList() ?? new();
    }

    public async Task<ProfileAttributeValue?> GetCvAttributeValueAsync(Guid cvId, Guid attributeDefinitionId)
    {
        var cv = await _db.CvRecords
            .Include(c => c.CandidateProfile).ThenInclude(p => p!.AttributeValues)
            .FirstOrDefaultAsync(c => c.Id == cvId);
        return cv?.CandidateProfile?.AttributeValues.FirstOrDefault(v => v.AttributeDefinitionId == attributeDefinitionId);
    }

    public async Task SaveAttributeValueAsync(Guid cvId, ProfileAttributeValue value)
    {
        var cv = await _db.CvRecords
            .Include(c => c.CandidateProfile).ThenInclude(p => p!.AttributeValues)
            .FirstOrDefaultAsync(c => c.Id == cvId) ?? throw new InvalidOperationException("CV not found");
        if (cv.CandidateProfile is null) throw new InvalidOperationException("Candidate profile not found");

        var existing = cv.CandidateProfile.AttributeValues
            .FirstOrDefault(v => v.AttributeDefinitionId == value.AttributeDefinitionId);
        if (existing is null)
        {
            existing = new ProfileAttributeValue
            {
                Id = Guid.NewGuid(),
                CandidateProfileId = cv.CandidateProfileId,
                AttributeDefinitionId = value.AttributeDefinitionId,
            };
            _db.ProfileAttributeValues.Add(existing);
        }
        existing.StringValue = value.StringValue;
        existing.TextValue = value.TextValue;
        existing.ImageUrl = value.ImageUrl;
        existing.NumericValue = value.NumericValue;
        existing.DateValue = value.DateValue;
        existing.PeriodStart = value.PeriodStart;
        existing.PeriodEnd = value.PeriodEnd;
        existing.BoolValue = value.BoolValue;
        existing.SelectedOptionId = value.SelectedOptionId;

        cv.CandidateProfile.UpdatedAt = DateTimeOffset.UtcNow;
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException("This profile was saved elsewhere in the meantime. Reload and try again.");
        }
    }

    public async Task<List<AttributeDefinition>> GetMissingRequiredAttributesAsync(Guid cvId)
    {
        var definitions = await GetCvAttributeDefinitionsAsync(cvId);
        var cv = await _db.CvRecords
            .Include(c => c.CandidateProfile).ThenInclude(p => p!.AttributeValues)
            .FirstOrDefaultAsync(c => c.Id == cvId);
        var values = cv?.CandidateProfile?.AttributeValues ?? new List<ProfileAttributeValue>();

        return definitions
            .Where(d => d.IsRequired && !IsValueFilled(values.FirstOrDefault(v => v.AttributeDefinitionId == d.Id)))
            .ToList();
    }

    public bool IsValueFilled(ProfileAttributeValue? value)
    {
        if (value is null) return false;
        return value.AttributeDefinition?.DataType switch
        {
            AttributeDataType.String => !string.IsNullOrWhiteSpace(value.StringValue),
            AttributeDataType.Text => !string.IsNullOrWhiteSpace(value.TextValue),
            AttributeDataType.Image => !string.IsNullOrWhiteSpace(value.ImageUrl),
            AttributeDataType.Numeric => value.NumericValue.HasValue,
            AttributeDataType.Date => value.DateValue.HasValue,
            AttributeDataType.Period => value.PeriodStart.HasValue,
            AttributeDataType.Boolean => value.BoolValue.HasValue,
            AttributeDataType.OneOfMany => value.SelectedOptionId.HasValue,
            _ => false
        };
    }

    public async Task<List<Project>> GetCvProjectsAsync(Guid cvId)
    {
        var cv = await _db.CvRecords
            .Include(c => c.CandidateProfile).ThenInclude(p => p!.CandidateProjects)
                .ThenInclude(cp => cp.Project).ThenInclude(p => p!.Tags)
            .Include(c => c.Position).ThenInclude(p => p!.Tags)
            .FirstOrDefaultAsync(c => c.Id == cvId);

        if (cv?.CandidateProfile is null || cv.Position is null) return new();

        var requiredTags = cv.Position.Tags.Select(t => t.Tag.ToLowerInvariant()).ToHashSet();
        var maxProjects = cv.Position.MaxProjects;

        return cv.CandidateProfile.CandidateProjects
            .Where(cp => cp.Project is not null)
            .Where(cp => !requiredTags.Any() || cp.Project!.Tags.Any(t => requiredTags.Contains(t.Tag.ToLowerInvariant())))
            .OrderByDescending(cp => cp.Project!.StartDate)
            .Take(maxProjects)
            .Select(cp => cp.Project!)
            .ToList();
    }
}

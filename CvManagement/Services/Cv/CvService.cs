using CvManagement.Data.Entities.Cv;
using CvManagement.Data.Entities.Profiles;
using CvManagement.Data.Entities.Positions;
using CvManagement.Data.Entities.Projects;
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
    Task<bool> CanPublishAsync(Guid id);
    Task<List<ProfileAttributeValue>> GetCvAttributeValuesAsync(Guid cvId);
    Task<List<Project>> GetCvProjectsAsync(Guid cvId);
}

public class CvService : ICvService
{
    private readonly Data.CvDbContext _db;

    public CvService(Data.CvDbContext db) => _db = db;

    public async Task<List<CvRecord>> GetCandidateCvsAsync(Guid candidateProfileId) =>
        await _db.CvRecords
            .Include(c => c.Position)
            .Include(c => c.Likes)
            .Where(c => c.CandidateProfileId == candidateProfileId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

    public async Task<List<CvRecord>> GetPublishedCvsForPositionAsync(Guid positionId) =>
        await _db.CvRecords
            .Include(c => c.CandidateProfile).ThenInclude(p => p!.User)
            .Include(c => c.Likes)
            .Where(c => c.PositionId == positionId && c.Status == CvStatus.Published)
            .OrderByDescending(c => c.LikeCount)
            .ToListAsync();

    public async Task<CvRecord?> GetByIdAsync(Guid id) =>
        await _db.CvRecords
            .Include(c => c.CandidateProfile).ThenInclude(p => p!.User)
            .Include(c => c.Position).ThenInclude(p => p!.AttributeRules).ThenInclude(r => r.AttributeDefinition)
            .Include(c => c.Position).ThenInclude(p => p!.Tags)
            .Include(c => c.Likes)
            .FirstOrDefaultAsync(c => c.Id == id);

    public async Task<CvRecord> CreateAsync(Guid candidateProfileId, Guid positionId, string title, Guid createdByUserId)
    {
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
        var cv = await _db.CvRecords.FindAsync(id)
            ?? throw new InvalidOperationException("CV not found");
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

    public async Task<bool> CanPublishAsync(Guid id)
    {
        var cv = await _db.CvRecords
            .Include(c => c.Position).ThenInclude(p => p!.AttributeRules)
            .Include(c => c.CandidateProfile).ThenInclude(p => p!.AttributeValues)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (cv?.Position is null || cv.CandidateProfile is null) return false;

        var requiredAttrIds = cv.Position.AttributeRules
            .Where(r => r.IsRequired)
            .Select(r => r.AttributeDefinitionId)
            .ToHashSet();

        var filledAttrIds = cv.CandidateProfile.AttributeValues
            .Where(v => HasValue(v))
            .Select(v => v.AttributeDefinitionId)
            .ToHashSet();

        return requiredAttrIds.IsSubsetOf(filledAttrIds);
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

    private static bool HasValue(ProfileAttributeValue v) =>
        v.StringValue is not null ||
        v.TextValue is not null ||
        v.ImageUrl is not null ||
        v.NumericValue.HasValue ||
        v.DateValue.HasValue ||
        v.PeriodStart.HasValue ||
        v.BoolValue.HasValue ||
        v.SelectedOptionId.HasValue;
}

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
    Task<CvRecord> CreateAsync(Guid candidateProfileId, Guid positionId, string title);
    Task<bool> CanEditAsync(Guid cvId);
    Task<bool> CanReadAsync(Guid cvId);
    Task DeleteAsync(Guid id);
    Task<CvRecord> PublishAsync(Guid id);
    Task<CvRecord> UnpublishAsync(Guid id);
    Task UpdateTitleAsync(Guid id, string title);
    Task<List<PositionAttributeRule>> GetCvAttributeRulesAsync(Guid cvId);
    Task<Dictionary<Guid, ProfileAttributeValue>> GetCvAttributeValueMapAsync(Guid cvId);
    Task SaveAttributeValueAsync(Guid cvId, ProfileAttributeValue value);
    Task SaveAttributeValuesAsync(Guid cvId, IEnumerable<ProfileAttributeValue> values);
    Task<List<PositionAttributeRule>> GetMissingRequiredRulesAsync(Guid cvId);
    Task<List<Project>> GetCvProjectsAsync(Guid cvId);
    bool IsValueFilled(ProfileAttributeValue? value);
    Task<List<CvRecord>> GetVisibleCvsForPositionAsync(Guid positionId);
}

public class CvService : ICvService
{
    private readonly Data.CvDbContext _db;
    private readonly Auth.ICurrentUserService _currentUser;

    public CvService(Data.CvDbContext db, Auth.ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    // CV mutations are limited to the owning candidate and to administrators (spec §3.6/§3.7).
    // Attribute writes land on the shared candidate profile, so an unchecked edit here would
    // let one user rewrite another user's profile data.
    public async Task<bool> CanEditAsync(Guid cvId) =>
        await _currentUser.IsInRoleAsync(RoleNames.Administrator)
        || await _currentUser.GetUserIdAsync() == await GetOwnerUserIdAsync(cvId);

    // Recruiters get read-only access; candidates only to their own CVs.
    public async Task<bool> CanReadAsync(Guid cvId) =>
        await CanEditAsync(cvId)
        || await _currentUser.IsInRoleAsync(RoleNames.Recruiter);

    private async Task<Guid> GetOwnerUserIdAsync(Guid cvId) =>
        await _db.CvRecords
            .Where(c => c.Id == cvId)
            .Select(c => c.CandidateProfile!.UserId)
            .FirstOrDefaultAsync();

    private async Task EnsureCanEditAsync(Guid cvId)
    {
        if (!await CanEditAsync(cvId))
            throw new UnauthorizedAccessException("You can only edit your own CVs.");
    }

    private async Task EnsureCanEditProfileAsync(Guid candidateProfileId)
    {
        if (await _currentUser.IsInRoleAsync(RoleNames.Administrator))
            return;

        var ownerUserId = await _db.CandidateProfiles
            .Where(p => p.Id == candidateProfileId)
            .Select(p => p.UserId)
            .FirstOrDefaultAsync();

        if (await _currentUser.GetUserIdAsync() != ownerUserId)
            throw new UnauthorizedAccessException("You can only act on your own profile.");
    }

    // Spec §7.4: candidates may only create CVs for positions they currently match. A Public
    // position is open to everyone signed in; only a Restricted position is filter-gated.
    private async Task EnsurePositionAccessibleAsync(Guid candidateProfileId, Guid positionId)
    {
        var isPublic = await _db.Positions
            .AsNoTracking()
            .Where(p => p.Id == positionId)
            .Select(p => (bool?)p.IsPublic)
            .FirstOrDefaultAsync() ?? false;

        if (isPublic) return;

        var rules = await _db.PositionAccessRules
            .AsNoTracking()
            .Include(r => r.AttributeDefinition)
            .Where(r => r.PositionId == positionId)
            .ToListAsync();
        if (rules.Count == 0)
            throw new UnauthorizedAccessException("This position is restricted and has no access rules.");

        var values = await _db.ProfileAttributeValues
            .AsNoTracking()
            .Include(v => v.SelectedOption)
            .Where(v => v.CandidateProfileId == candidateProfileId)
            .ToListAsync();

        if (!AccessRuleEvaluator.CanAccess(rules, values))
            throw new UnauthorizedAccessException("You do not currently have access to this position.");
    }

    public async Task<List<CvRecord>> GetCandidateCvsAsync(Guid candidateProfileId)
    {
        var cvs = await _db.CvRecords
            .Include(c => c.Position)
            .Include(c => c.Likes)
            .Include(c => c.Position!.AccessRules).ThenInclude(r => r.AttributeDefinition)
            .Where(c => c.CandidateProfileId == candidateProfileId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        // Hide CVs for restricted positions the candidate can no longer access (§7.4),
        // including from the owner's own view. Public positions are always visible.
        var restricted = cvs.Where(c => c.Position is not null && !c.Position.IsPublic).ToList();
        if (restricted.Count == 0) return cvs;

        var rulesByPosition = restricted
            .Where(c => c.Position.AccessRules.Count > 0)
            .GroupBy(c => c.PositionId)
            .ToDictionary(g => g.Key, g => g.First().Position.AccessRules);

        var candidateIds = restricted.Select(c => c.CandidateProfileId).Distinct().ToList();
        var valuesByCandidate = (await _db.ProfileAttributeValues
                .AsNoTracking()
                .Include(v => v.SelectedOption)
                .Where(v => candidateIds.Contains(v.CandidateProfileId))
                .ToListAsync())
            .GroupBy(v => v.CandidateProfileId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return cvs
            .Where(c => c.Position is null
                        || c.Position.IsPublic
                        || (rulesByPosition.TryGetValue(c.PositionId, out var rules)
                            && AccessRuleEvaluator.CanAccess(rules, valuesByCandidate.GetValueOrDefault(c.CandidateProfileId) ?? [])))
            .ToList();
    }

    public async Task<List<CvRecord>> GetPublishedCvsForPositionAsync(Guid positionId) =>
        await _db.CvRecords
            .Include(c => c.CandidateProfile).ThenInclude(p => p!.User)
            .Include(c => c.Likes)
            .Where(c => c.PositionId == positionId && c.Status == CvStatus.Published)
            .OrderByDescending(c => c.LikeCount)
            .ToListAsync();

    // Access rules and candidate values are fetched once, then evaluated in memory.
    // The previous version re-queried both per CV, i.e. 2 round-trips per row.
    public async Task<List<CvRecord>> GetVisibleCvsForPositionAsync(Guid positionId)
    {
        var cvs = await GetPublishedCvsForPositionAsync(positionId);
        if (cvs.Count == 0) return cvs;

        var rules = await _db.PositionAccessRules
            .AsNoTracking()
            .Include(r => r.AttributeDefinition)
            .Where(r => r.PositionId == positionId)
            .ToListAsync();

        var isPublic = await _db.Positions
            .AsNoTracking()
            .Where(p => p.Id == positionId)
            .Select(p => p.IsPublic)
            .FirstOrDefaultAsync();

        // A Public position has no gate; a Restricted one with no rules is closed.
        if (isPublic || rules.Count == 0) return isPublic ? cvs : [];

        var candidateIds = cvs.Select(c => c.CandidateProfileId).Distinct().ToList();
        var valuesByCandidate = (await _db.ProfileAttributeValues
                .AsNoTracking()
                .Include(v => v.SelectedOption)
                .Where(v => candidateIds.Contains(v.CandidateProfileId))
                .ToListAsync())
            .GroupBy(v => v.CandidateProfileId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return cvs
            .Where(c => AccessRuleEvaluator.CanAccess(
                rules, valuesByCandidate.GetValueOrDefault(c.CandidateProfileId) ?? []))
            .ToList();
    }

    public async Task<CvRecord?> GetByIdAsync(Guid id) =>
        await _db.CvRecords
            .Include(c => c.CandidateProfile).ThenInclude(p => p!.User)
            .Include(c => c.Position).ThenInclude(p => p!.AttributeRules).ThenInclude(r => r.AttributeDefinition)
            .Include(c => c.Position).ThenInclude(p => p!.Tags)
            .Include(c => c.Likes)
            .FirstOrDefaultAsync(c => c.Id == id);

    public async Task<CvRecord> CreateAsync(Guid candidateProfileId, Guid positionId, string title)
    {
        await EnsureCanEditProfileAsync(candidateProfileId);
        await EnsurePositionAccessibleAsync(candidateProfileId, positionId);

        var exists = await _db.CvRecords
            .AnyAsync(c => c.CandidateProfileId == candidateProfileId && c.PositionId == positionId);
        if (exists)
            throw new InvalidOperationException("A CV for this position already exists.");

        var createdByUserId = await _currentUser.GetUserIdAsync() ?? Guid.Empty;


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
        await EnsureCanEditAsync(id);

        var cv = await _db.CvRecords.FindAsync(id)
            ?? throw new InvalidOperationException("CV not found");
        _db.CvRecords.Remove(cv);
        await _db.SaveChangesAsync();
    }

    public async Task<CvRecord> PublishAsync(Guid id)
    {
        await EnsureCanEditAsync(id);

        var cv = await _db.CvRecords.FindAsync(id)
            ?? throw new InvalidOperationException("CV not found");

        var missing = await GetMissingRequiredRulesAsync(id);
        if (missing.Count > 0)
            throw new InvalidOperationException("Not all required attributes are filled.");

        cv.Status = CvStatus.Published;
        await _db.SaveChangesAsync();
        return cv;
    }

    public async Task<CvRecord> UnpublishAsync(Guid id)
    {
        await EnsureCanEditAsync(id);

        var cv = await _db.CvRecords.FindAsync(id)
            ?? throw new InvalidOperationException("CV not found");
        cv.Status = CvStatus.Draft;
        await _db.SaveChangesAsync();
        return cv;
    }

    public async Task UpdateTitleAsync(Guid id, string title)
    {
        await EnsureCanEditAsync(id);

        var cv = await _db.CvRecords.FindAsync(id)
            ?? throw new InvalidOperationException("CV not found");
        cv.Title = title;
        await _db.SaveChangesAsync();
    }

    // Returns the position's template rules, carrying the per-position IsRequired flag
    // that gates publishing (spec §8.4).
    public async Task<List<PositionAttributeRule>> GetCvAttributeRulesAsync(Guid cvId)
    {
        var rules = await _db.PositionAttributeRules
            .Include(r => r.AttributeDefinition).ThenInclude(d => d.Options)
            .Where(r => r.Position!.CvRecords.Any(c => c.Id == cvId))
            .ToListAsync();
        return rules.OrderBy(r => r.SortOrder).ToList();
    }

    // One query for the whole position template instead of one per attribute definition.
    public async Task<Dictionary<Guid, ProfileAttributeValue>> GetCvAttributeValueMapAsync(Guid cvId)
    {
        var values = await _db.ProfileAttributeValues
            .Include(v => v.SelectedOption)
            .Include(v => v.AttributeDefinition)
            .Where(v => v.CandidateProfile!.CvRecords.Any(c => c.Id == cvId))
            .ToListAsync();

        return values.ToDictionary(v => v.AttributeDefinitionId);
    }

    public async Task SaveAttributeValueAsync(Guid cvId, ProfileAttributeValue value) =>
        await SaveAttributeValuesAsync(cvId, [value]);

    // One transaction for the whole form. Saving attribute-by-attribute left the CV in a
    // half-written state whenever a later attribute failed.
    public async Task SaveAttributeValuesAsync(Guid cvId, IEnumerable<ProfileAttributeValue> values)
    {
        await EnsureCanEditAsync(cvId);

        var candidateProfile = await _db.CandidateProfiles
            .Include(p => p.AttributeValues)
            .FirstOrDefaultAsync(p => p.CvRecords.Any(c => c.Id == cvId))
            ?? throw new InvalidOperationException("CV not found");

        var existingByAttribute = candidateProfile.AttributeValues.ToDictionary(v => v.AttributeDefinitionId);

        foreach (var value in values)
        {
            if (!existingByAttribute.TryGetValue(value.AttributeDefinitionId, out var existing))
            {
                existing = new ProfileAttributeValue
                {
                    Id = Guid.NewGuid(),
                    CandidateProfileId = candidateProfile.Id,
                    AttributeDefinitionId = value.AttributeDefinitionId
                };
                _db.ProfileAttributeValues.Add(existing);
                existingByAttribute[value.AttributeDefinitionId] = existing;
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
        }

        candidateProfile.UpdatedAt = DateTimeOffset.UtcNow;
        await SaveWithConflictTranslationAsync();
    }

    // Publish is gated on the position's own required flags (spec §8.4), not the
    // library-wide AttributeDefinition.IsRequired.
    public async Task<List<PositionAttributeRule>> GetMissingRequiredRulesAsync(Guid cvId)
    {
        var rules = await _db.PositionAttributeRules
            .Include(r => r.AttributeDefinition)
            .Where(r => r.Position!.CvRecords.Any(c => c.Id == cvId))
            .ToListAsync();

        var values = await GetCandidateValuesAsync(cvId);

        return rules
            .Where(r => r.IsRequired && !IsValueFilled(values.GetValueOrDefault(r.AttributeDefinitionId)))
            .ToList();
    }

    private async Task<Dictionary<Guid, ProfileAttributeValue>> GetCandidateValuesAsync(Guid cvId) =>
        await _db.ProfileAttributeValues
            .Include(v => v.SelectedOption)
            .Include(v => v.AttributeDefinition)
            .Where(v => v.CandidateProfile!.CvRecords.Any(c => c.Id == cvId))
            .ToDictionaryAsync(v => v.AttributeDefinitionId);

    private async Task SaveWithConflictTranslationAsync()
    {
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException(
                "This profile was saved elsewhere in the meantime. Reload and try again.");
        }
    }

    public bool IsValueFilled(ProfileAttributeValue? value)
    {
        if (value is null || value.AttributeDefinition is null) return false;
        return value.AttributeDefinition.DataType switch
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

        // Spec §13.3: a project qualifies only if it carries *every* required tag.
        var requiredTags = cv.Position.Tags.Select(t => t.Tag.ToLowerInvariant()).ToHashSet();
        var maxProjects = cv.Position.MaxProjects;

        return cv.CandidateProfile.CandidateProjects
            .Where(cp => cp.Project is not null)
            .Where(cp => requiredTags.All(
                required => cp.Project!.Tags.Any(t => string.Equals(t.Tag, required, StringComparison.OrdinalIgnoreCase))))
            // Undated projects sort last: Postgres orders NULLs first on DESC.
            .OrderByDescending(cp => cp.Project!.StartDate ?? DateTime.MinValue)
            .Take(maxProjects)
            .Select(cp => cp.Project!)
            .ToList();
    }
}

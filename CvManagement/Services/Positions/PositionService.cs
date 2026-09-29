using CvManagement.Data;
using CvManagement.Data.Entities.Positions;
using CvManagement.Services.Concurrency;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Services.Positions;

public class PositionService : IPositionService
{
    private const string EntityName = "This position";

    private readonly CvDbContext _db;

    public PositionService(CvDbContext db) => _db = db;

    // Reads never track: a Blazor circuit's scoped context would otherwise keep serving
    // the entity it loaded on first render, defeating the version check on save.
    public Task<List<Position>> GetAllAsync(bool? publicOnly = null) =>
        _db.Positions
            .AsNoTracking()
            .Include(p => p.AttributeRules).ThenInclude(r => r.AttributeDefinition)
            .Include(p => p.Tags)
            .Include(p => p.AccessRules).ThenInclude(r => r.AttributeDefinition)
            .Include(p => p.CreatedByUser)
            .Where(p => publicOnly == null || p.IsPublic == publicOnly)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

    public async Task<Position> DuplicateAsync(Guid id, Guid createdByUserId)
    {
        var source = await _db.Positions
            .AsNoTracking()
            .Include(p => p.AttributeRules)
            .Include(p => p.Tags)
            .Include(p => p.AccessRules)
            .FirstOrDefaultAsync(p => p.Id == id) ?? throw new InvalidOperationException("Position not found");

        var copy = new Position
        {
            Id = Guid.NewGuid(),
            Title = $"{source.Title} (copy)",
            Description = source.Description,
            Company = source.Company,
            IsPublic = source.IsPublic,
            IsOpen = source.IsOpen,
            MaxProjects = source.MaxProjects,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            AttributeRules = source.AttributeRules
                .OrderBy(r => r.SortOrder)
                .Select(r => new PositionAttributeRule
                {
                    AttributeDefinitionId = r.AttributeDefinitionId,
                    IsRequired = r.IsRequired,
                    SortOrder = r.SortOrder
                }).ToList(),
            Tags = source.Tags.Select(t => new PositionTag { Tag = t.Tag }).ToList(),
            AccessRules = source.AccessRules.Select(a => new PositionAccessRule
            {
                AttributeDefinitionId = a.AttributeDefinitionId,
                Operator = a.Operator,
                FilterValue = a.FilterValue
            }).ToList(),
        };

        _db.Positions.Add(copy);
        await _db.SaveChangesAsync();
        return copy;
    }

    // Spec §5.3: a Public position is open to any authenticated user. Filters only narrow
    // a Restricted position. No rules on a restricted position means nobody passes.
    public async Task<bool> CanCandidateAccessAsync(Guid candidateProfileId, Guid positionId)
    {
        var isPublic = await _db.Positions
            .AsNoTracking()
            .Where(p => p.Id == positionId)
            .Select(p => (bool?)p.IsPublic)
            .FirstOrDefaultAsync() ?? false;

        if (isPublic) return true;

        var rules = await _db.PositionAccessRules
            .AsNoTracking()
            .Include(r => r.AttributeDefinition)
            .Where(r => r.PositionId == positionId)
            .ToListAsync();
        if (rules.Count == 0) return false;

        var values = await _db.ProfileAttributeValues
            .AsNoTracking()
            .Include(v => v.SelectedOption)
            .Where(v => v.CandidateProfileId == candidateProfileId)
            .ToListAsync();
        return AccessRuleEvaluator.CanAccess(rules, values);
    }

    public async Task<List<Position>> GetAccessiblePositionsAsync(Guid candidateProfileId)
    {
        var positions = await GetAllAsync();
        var restricted = positions.Where(p => !p.IsPublic).ToList();
        if (restricted.Count == 0) return positions;

        var values = await _db.ProfileAttributeValues
            .AsNoTracking()
            .Include(v => v.SelectedOption)
            .Where(v => v.CandidateProfileId == candidateProfileId)
            .ToListAsync();

        // ponytail: in-memory evaluation, fine for demo scale; push to a SQL join when data grows
        return positions.Where(p => p.IsPublic || AccessRuleEvaluator.CanAccess(p.AccessRules, values)).ToList();
    }

    public Task<List<Position>> GetLatestAsync(int count) =>
        _db.Positions
            .AsNoTracking()
            .Include(p => p.Tags)
            .Include(p => p.CvRecords)
            .OrderByDescending(p => p.CreatedAt)
            .Take(count)
            .ToListAsync();

    public Task<List<Position>> GetMostPopularAsync(int count) =>
        _db.Positions
            .AsNoTracking()
            .Include(p => p.Tags)
            .Include(p => p.CvRecords)
            .Where(p => p.CvRecords.Any())
            .OrderByDescending(p => p.CvRecords.Count)
            .ThenByDescending(p => p.CreatedAt)
            .Take(count)
            .ToListAsync();

    public async Task<List<(string Tag, int Count)>> GetTagCountsAsync()
    {
        var tags = await _db.PositionTags
            .GroupBy(t => t.Tag)
            .OrderByDescending(g => g.Count())
            .Select(g => new { Tag = g.Key, Count = g.Count() })
            .Take(30)
            .ToListAsync();
        return tags.Select(x => (x.Tag, x.Count)).ToList();
    }

    public async Task<(int Positions, int Candidates, int Recruiters, int Cvs24h, int TotalCvs)> GetLandingStatsAsync()
    {
        var cutOff = DateTimeOffset.UtcNow.AddHours(-24);
        var candidateRoleId = await GetRoleIdAsync(RoleNames.Candidate);
        var recruiterRoleId = await GetRoleIdAsync(RoleNames.Recruiter);
        var positions = await _db.Positions.CountAsync();
        var candidates = candidateRoleId.HasValue
            ? await _db.UserRoles.CountAsync(ur => ur.RoleId == candidateRoleId.Value)
            : 0;
        var recruiters = recruiterRoleId.HasValue
            ? await _db.UserRoles.CountAsync(ur => ur.RoleId == recruiterRoleId.Value)
            : 0;
        var cvs24h = await _db.CvRecords.CountAsync(c => c.CreatedAt >= cutOff);
        var totalCvs = await _db.CvRecords.CountAsync();
        return (positions, candidates, recruiters, cvs24h, totalCvs);
    }

    // Feeds the tag-input autocomplete, which suggests tags already used anywhere in the system.
    public async Task<List<string>> GetAllTagNamesAsync() =>
        await _db.PositionTags
            .AsNoTracking()
            .Select(t => t.Tag)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync();

    private Task<Guid?> GetRoleIdAsync(string role) =>
        _db.Roles
            .AsNoTracking()
            .Where(r => r.NormalizedName == RoleNames.ToNormalized(role))
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync();

    public Task<Position?> GetByIdAsync(Guid id) =>
        _db.Positions
            .AsNoTracking()
            .Include(p => p.AttributeRules).ThenInclude(r => r.AttributeDefinition)
            .Include(p => p.Tags)
            .Include(p => p.AccessRules).ThenInclude(r => r.AttributeDefinition)
            .Include(p => p.CreatedByUser)
            .Include(p => p.CvRecords)
            .FirstOrDefaultAsync(p => p.Id == id);

    public async Task<Position> CreateAsync(Position position, Guid createdByUserId)
    {
        position.Id = Guid.NewGuid();
        position.CreatedByUserId = createdByUserId;
        position.CreatedAt = DateTimeOffset.UtcNow;

        position.AccessRules = position.IsPublic ? [] : NormalizeAccessRules(position).ToList();
        foreach (var rule in position.AccessRules)
            rule.PositionId = position.Id;

        // The edit form carries attribute definitions purely for display; clearing the
        // navigation keeps EF from treating the shared definitions as new rows.
        foreach (var rule in position.AttributeRules)
        {
            rule.PositionId = position.Id;
            rule.AttributeDefinition = null!;
        }

        foreach (var rule in position.AccessRules)
            rule.AttributeDefinition = null!;

        _db.Positions.Add(position);
        await _db.SaveChangesAsync();
        return position;
    }

    // Optimistic locking (spec §4). Attribute template and access rules travel with the
    // position so the whole form is written as one graph in a single save.
    public async Task<Position> UpdateAsync(Guid id, Position position, byte[] expectedVersion)
    {
        var existing = await _db.Positions
            .Include(p => p.AccessRules)
            .Include(p => p.AttributeRules)
            .FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new InvalidOperationException("Position not found");

        if (!existing.RowVersion.AsSpan().SequenceEqual(expectedVersion))
            throw new ConcurrencyConflictException(EntityName);

        existing.Title = position.Title;
        existing.Description = position.Description;
        existing.Company = position.Company;
        existing.IsPublic = position.IsPublic;
        existing.IsOpen = position.IsOpen;
        existing.MaxProjects = position.MaxProjects;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        SyncAttributeRules(existing, position.AttributeRules);

        // A Public position keeps no filters: they would be misleading and unreachable.
        _db.PositionAccessRules.RemoveRange(existing.AccessRules);
        if (!position.IsPublic)
        {
            foreach (var rule in NormalizeAccessRules(position))
                _db.PositionAccessRules.Add(new PositionAccessRule
                {
                    PositionId = id,
                    AttributeDefinitionId = rule.AttributeDefinitionId,
                    Operator = rule.Operator,
                    FilterValue = rule.FilterValue
                });
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException(EntityName);
        }

        return existing;
    }

    // Changes a position's attribute template without ever rewriting CVs: existing CVs
    // resolve their content from the profile through the current template (spec §13.2).
    private static void SyncAttributeRules(Position existing, IEnumerable<PositionAttributeRule> desiredRules)
    {
        var desired = desiredRules
            .GroupBy(r => r.AttributeDefinitionId)
            .Select(g => g.First())
            .ToList();

        var desiredIds = desired.Select(r => r.AttributeDefinitionId).ToHashSet();

        foreach (var stale in existing.AttributeRules
                     .Where(r => !desiredIds.Contains(r.AttributeDefinitionId))
                     .ToList())
        {
            existing.AttributeRules.Remove(stale);
        }

        var presentIds = existing.AttributeRules.Select(r => r.AttributeDefinitionId).ToHashSet();
        var nextSortOrder = existing.AttributeRules.Count;

        foreach (var rule in existing.AttributeRules)
        {
            var match = desired.FirstOrDefault(d => d.AttributeDefinitionId == rule.AttributeDefinitionId);
            if (match is not null)
                rule.IsRequired = match.IsRequired;
        }

        foreach (var rule in desired.Where(r => !presentIds.Contains(r.AttributeDefinitionId)))
        {
            existing.AttributeRules.Add(new PositionAttributeRule
            {
                PositionId = existing.Id,
                AttributeDefinitionId = rule.AttributeDefinitionId,
                IsRequired = rule.IsRequired,
                SortOrder = nextSortOrder++
            });
        }
    }

    private static List<PositionAccessRule> NormalizeAccessRules(Position position)
    {
        var seen = new HashSet<Guid>();
        return position.AccessRules
            .Where(r => seen.Add(r.AttributeDefinitionId))
            .Select(r =>
            {
                r.PositionId = position.Id;
                return r;
            })
            .ToList();
    }

    public async Task DeleteAsync(Guid id)
    {
        var position = await _db.Positions.FindAsync(id)
            ?? throw new InvalidOperationException("Position not found");
        _db.Positions.Remove(position);
        await _db.SaveChangesAsync();
    }

    // One load and one save for a multi-row toolbar delete; related rows go with it via the
    // database's cascade delete (spec §13.4).
    public async Task DeleteManyAsync(IReadOnlyCollection<Guid> ids)
    {
        if (ids.Count == 0)
            return;

        var positions = await _db.Positions
            .Where(p => ids.Contains(p.Id))
            .ToListAsync();

        _db.Positions.RemoveRange(positions);
        await _db.SaveChangesAsync();
    }

    // One diff + one save for the whole tag set. Writing tag-by-tag issued a round-trip
    // per tag (the spec's "no queries in loops").
    public async Task SetTagsAsync(Guid positionId, IReadOnlyCollection<string> tags)
    {
        var desired = NormalizeTags(tags);

        var existing = await _db.PositionTags
            .Where(t => t.PositionId == positionId)
            .ToListAsync();

        _db.PositionTags.RemoveRange(existing.Where(t => !desired.Contains(t.Tag)));

        var currentTags = existing.Select(t => t.Tag).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in desired.Where(tag => !currentTags.Contains(tag)))
            _db.PositionTags.Add(new PositionTag { PositionId = positionId, Tag = tag });

        await _db.SaveChangesAsync();
    }

    private static HashSet<string> NormalizeTags(IEnumerable<string> tags) =>
        tags.Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}

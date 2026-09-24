using CvManagement.Data;
using CvManagement.Data.Entities.Positions;
using CvManagement.Data.Entities.Profiles;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Services.Positions;

public class PositionService : IPositionService
{
    private readonly CvDbContext _db;

    public PositionService(CvDbContext db) => _db = db;

    public async Task<List<Position>> GetAllAsync(bool? publicOnly = null) =>
        await _db.Positions
            .Include(p => p.AttributeRules).ThenInclude(r => r.AttributeDefinition)
            .Include(p => p.Tags)
            .Include(p => p.AccessRules)
            .Include(p => p.CreatedByUser)
            .Where(p => publicOnly == null || p.IsPublic == publicOnly)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

    public async Task<Position> DuplicateAsync(Guid id, Guid createdByUserId)
    {
        var source = await _db.Positions
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

    public async Task<bool> CanCandidateAccessAsync(Guid candidateProfileId, Guid positionId)
    {
        var rules = await _db.PositionAccessRules
            .Include(r => r.AttributeDefinition)
            .Where(r => r.PositionId == positionId)
            .ToListAsync();
        if (rules.Count == 0) return true;

        var values = await _db.ProfileAttributeValues
            .Include(v => v.SelectedOption)
            .Where(v => v.CandidateProfileId == candidateProfileId)
            .ToListAsync();
        return AccessRuleEvaluator.CanAccess(rules, values);
    }

    public async Task<List<Position>> GetAccessiblePositionsAsync(Guid candidateProfileId)
    {
        var positions = await GetAllAsync();
        var values = await _db.ProfileAttributeValues
            .Include(v => v.SelectedOption)
            .Where(v => v.CandidateProfileId == candidateProfileId)
            .ToListAsync();
        // ponytail: in-memory evaluation, fine for demo scale; push to SQL join when data grows
        return positions.Where(p => !p.AccessRules.Any() || AccessRuleEvaluator.CanAccess(p.AccessRules, values)).ToList();
    }

    public async Task<List<Position>> GetLatestAsync(int count) =>
        await _db.Positions
            .Include(p => p.Tags)
            .Include(p => p.CvRecords)
            .OrderByDescending(p => p.CreatedAt)
            .Take(count)
            .ToListAsync();

    public async Task<List<Position>> GetMostPopularAsync(int count) =>
        await _db.Positions
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
        var candidateRoleId = (await _db.Roles.FirstOrDefaultAsync(r => r.NormalizedName == "CANDIDATE"))?.Id;
        var recruiterRoleId = (await _db.Roles.FirstOrDefaultAsync(r => r.NormalizedName == "RECRUITER"))?.Id;
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

    public async Task<Position?> GetByIdAsync(Guid id) =>
        await _db.Positions
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
        _db.Positions.Add(position);
        await _db.SaveChangesAsync();
        return position;
    }

    public async Task<Position> UpdateAsync(Guid id, Position position)
    {
        var existing = await _db.Positions.FindAsync(id)
            ?? throw new InvalidOperationException("Position not found");

        existing.Title = position.Title;
        existing.Description = position.Description;
        existing.Company = position.Company;
        existing.IsPublic = position.IsPublic;
        existing.IsOpen = position.IsOpen;
        existing.MaxProjects = position.MaxProjects;

        await _db.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAsync(Guid id)
    {
        var position = await _db.Positions.FindAsync(id)
            ?? throw new InvalidOperationException("Position not found");
        _db.Positions.Remove(position);
        await _db.SaveChangesAsync();
    }

    public async Task AddAttributeRuleAsync(Guid positionId, Guid attributeId, bool isRequired)
    {
        var exists = await _db.PositionAttributeRules
            .AnyAsync(r => r.PositionId == positionId && r.AttributeDefinitionId == attributeId);
        if (exists) return;

        var sortOrder = await _db.PositionAttributeRules.CountAsync(r => r.PositionId == positionId);
        _db.PositionAttributeRules.Add(new PositionAttributeRule
        {
            PositionId = positionId,
            AttributeDefinitionId = attributeId,
            IsRequired = isRequired,
            SortOrder = sortOrder
        });
        await _db.SaveChangesAsync();
    }

    public async Task RemoveAttributeRuleAsync(Guid positionId, Guid attributeId)
    {
        var rule = await _db.PositionAttributeRules
            .FirstOrDefaultAsync(r => r.PositionId == positionId && r.AttributeDefinitionId == attributeId);
        if (rule is not null)
        {
            _db.PositionAttributeRules.Remove(rule);
            await _db.SaveChangesAsync();
        }
    }

    public async Task AddTagAsync(Guid positionId, string tag)
    {
        var exists = await _db.PositionTags
            .AnyAsync(t => t.PositionId == positionId && t.Tag == tag);
        if (exists) return;

        _db.PositionTags.Add(new PositionTag { PositionId = positionId, Tag = tag });
        await _db.SaveChangesAsync();
    }

    public async Task RemoveTagAsync(Guid positionId, string tag)
    {
        var tagEntity = await _db.PositionTags
            .FirstOrDefaultAsync(t => t.PositionId == positionId && t.Tag == tag);
        if (tagEntity is not null)
        {
            _db.PositionTags.Remove(tagEntity);
            await _db.SaveChangesAsync();
        }
    }
}

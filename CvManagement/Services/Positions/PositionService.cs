using CvManagement.Data;
using CvManagement.Data.Entities.Positions;
using CvManagement.Data.Entities.Attributes;
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

    public async Task<Position> DuplicateAsync(Guid sourceId, Guid createdByUserId)
    {
        var source = await GetByIdAsync(sourceId)
            ?? throw new InvalidOperationException("Source position not found");

        var copy = new Position
        {
            Title = source.Title + " (Copy)",
            Description = source.Description,
            Company = source.Company,
            IsPublic = source.IsPublic,
            IsOpen = source.IsOpen,
            MaxProjects = source.MaxProjects,
            CreatedByUserId = createdByUserId
        };

        _db.Positions.Add(copy);
        await _db.SaveChangesAsync();

        foreach (var rule in source.AttributeRules)
        {
            _db.PositionAttributeRules.Add(new PositionAttributeRule
            {
                PositionId = copy.Id,
                AttributeDefinitionId = rule.AttributeDefinitionId,
                IsRequired = rule.IsRequired,
                SortOrder = rule.SortOrder
            });
        }

        foreach (var tag in source.Tags)
        {
            _db.PositionTags.Add(new PositionTag
            {
                PositionId = copy.Id,
                Tag = tag.Tag
            });
        }

        foreach (var accessRule in source.AccessRules)
        {
            _db.PositionAccessRules.Add(new PositionAccessRule
            {
                PositionId = copy.Id,
                AttributeDefinitionId = accessRule.AttributeDefinitionId,
                Operator = accessRule.Operator,
                FilterValue = accessRule.FilterValue
            });
        }

        await _db.SaveChangesAsync();
        return copy;
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

    public async Task AddAccessRuleAsync(Guid positionId, Guid attributeId, PositionAccessOperator op, string filterValue)
    {
        _db.PositionAccessRules.Add(new PositionAccessRule
        {
            PositionId = positionId,
            AttributeDefinitionId = attributeId,
            Operator = op,
            FilterValue = filterValue
        });
        await _db.SaveChangesAsync();
    }

    public async Task RemoveAccessRuleAsync(Guid positionId, Guid ruleId)
    {
        var rule = await _db.PositionAccessRules
            .FirstOrDefaultAsync(r => r.Id == ruleId && r.PositionId == positionId);
        if (rule is not null)
        {
            _db.PositionAccessRules.Remove(rule);
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

    public async Task<List<Position>> GetAccessiblePositionsAsync(Guid userId) =>
        await _db.Positions
            .Where(p => p.IsPublic || p.AccessRules.Any())
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

    public async Task<int> GetCvCountAsync(Guid positionId) =>
        await _db.CvRecords.CountAsync(c => c.PositionId == positionId);
}

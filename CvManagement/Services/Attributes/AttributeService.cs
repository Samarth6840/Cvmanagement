using CvManagement.Data;
using CvManagement.Data.Entities.Attributes;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Services.Attributes;

public class AttributeService : IAttributeService
{
    private readonly CvDbContext _db;

    public AttributeService(CvDbContext db) => _db = db;

    public async Task<List<AttributeDefinition>> GetAllAsync() =>
        await _db.AttributeDefinitions
            .OrderBy(a => a.SortOrder)
            .ThenBy(a => a.Name)
            .ToListAsync();

    public async Task<AttributeDefinition?> GetByIdAsync(Guid id) =>
        await _db.AttributeDefinitions
            .Include(a => a.Options)
            .FirstOrDefaultAsync(a => a.Id == id);

    public async Task<AttributeDefinition> CreateAsync(AttributeDefinition attribute)
    {
        attribute.Slug = attribute.Name.ToLowerInvariant().Replace(" ", "-");
        if (await _db.AttributeDefinitions.AnyAsync(a => a.Name == attribute.Name || a.Slug == attribute.Slug))
            throw new InvalidOperationException("An attribute with this name already exists.");

        attribute.Id = Guid.NewGuid();
        attribute.CreatedAt = DateTimeOffset.UtcNow;
        _db.AttributeDefinitions.Add(attribute);
        await _db.SaveChangesAsync();
        return attribute;
    }

    public async Task<AttributeDefinition> UpdateAsync(Guid id, AttributeDefinition attribute)
    {
        var existing = await _db.AttributeDefinitions.FindAsync(id)
            ?? throw new InvalidOperationException("Attribute not found");

        var slug = attribute.Name.ToLowerInvariant().Replace(" ", "-");
        if (await _db.AttributeDefinitions.AnyAsync(a => a.Id != id && (a.Name == attribute.Name || a.Slug == slug)))
            throw new InvalidOperationException("An attribute with this name already exists.");

        existing.Name = attribute.Name;
        existing.Slug = slug;
        existing.Category = attribute.Category;
        existing.DataType = attribute.DataType;
        existing.Description = attribute.Description;
        existing.IsRequired = attribute.IsRequired;
        existing.SortOrder = attribute.SortOrder;

        await _db.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAsync(Guid id)
    {
        var attribute = await _db.AttributeDefinitions.FindAsync(id)
            ?? throw new InvalidOperationException("Attribute not found");
        _db.AttributeDefinitions.Remove(attribute);
        await _db.SaveChangesAsync();
    }

    public async Task<List<AttributeDefinition>> GetRecentlyUsedAsync(int count)
    {
        var ids = await _db.PositionAttributeRules
            .OrderByDescending(r => r.Position!.CreatedAt)
            .Select(r => r.AttributeDefinitionId)
            .Distinct()
            .Take(count)
            .ToListAsync();
        var attributes = await _db.AttributeDefinitions
            .Where(a => ids.Contains(a.Id))
            .ToListAsync();
        return attributes.OrderBy(a => ids.IndexOf(a.Id)).ToList();
    }

    public async Task<List<AttributeOption>> GetOptionsAsync(Guid attributeId) =>
        await _db.AttributeOptions
            .Where(o => o.AttributeDefinitionId == attributeId)
            .OrderBy(o => o.SortOrder)
            .ToListAsync();

    public async Task<AttributeOption> AddOptionAsync(Guid attributeId, string label)
    {
        var option = new AttributeOption
        {
            AttributeDefinitionId = attributeId,
            Label = label,
            SortOrder = await _db.AttributeOptions.CountAsync(o => o.AttributeDefinitionId == attributeId)
        };
        _db.AttributeOptions.Add(option);
        await _db.SaveChangesAsync();
        return option;
    }

    public async Task RemoveOptionAsync(Guid attributeId, Guid optionId)
    {
        var option = await _db.AttributeOptions.FindAsync(optionId);
        if (option is not null && option.AttributeDefinitionId == attributeId)
        {
            _db.AttributeOptions.Remove(option);
            await _db.SaveChangesAsync();
        }
    }
}

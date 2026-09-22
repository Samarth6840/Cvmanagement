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
        attribute.Id = Guid.NewGuid();
        attribute.Slug = attribute.Name.ToLowerInvariant().Replace(" ", "-");
        attribute.CreatedAt = DateTimeOffset.UtcNow;
        _db.AttributeDefinitions.Add(attribute);
        await _db.SaveChangesAsync();
        return attribute;
    }

    public async Task<AttributeDefinition> UpdateAsync(Guid id, AttributeDefinition attribute)
    {
        var existing = await _db.AttributeDefinitions.FindAsync(id)
            ?? throw new InvalidOperationException("Attribute not found");

        existing.Name = attribute.Name;
        existing.Slug = attribute.Name.ToLowerInvariant().Replace(" ", "-");
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

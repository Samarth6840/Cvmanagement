using CvManagement.Data;
using CvManagement.Data.Entities.Attributes;
using CvManagement.Services.Concurrency;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Services.Attributes;

public class AttributeService : IAttributeService
{
    private const string EntityName = "This attribute";
    private const string BuiltInProtectedMessage = "Built-in profile attributes cannot be removed or retyped.";

    private readonly CvDbContext _db;

    public AttributeService(CvDbContext db) => _db = db;

    public Task<List<AttributeDefinition>> GetAllAsync() =>
        _db.AttributeDefinitions
            .AsNoTracking()
            .Include(a => a.Category)
            .Include(a => a.Options)
            .OrderBy(a => a.SortOrder)
            .ThenBy(a => a.Name)
            .ToListAsync();

    public Task<List<AttributeCategory>> GetCategoriesAsync() =>
        _db.AttributeCategories
            .AsNoTracking()
            .OrderBy(c => c.SortOrder)
            .ToListAsync();

    public Task<AttributeDefinition?> GetByIdAsync(Guid id) =>
        _db.AttributeDefinitions
            .AsNoTracking()
            .Include(a => a.Options)
            .Include(a => a.Category)
            .FirstOrDefaultAsync(a => a.Id == id);

    public async Task<AttributeDefinition> CreateAsync(AttributeDefinition attribute)
    {
        attribute.Slug = ToSlug(attribute.Name);
        if (await _db.AttributeDefinitions.AnyAsync(a => a.Name == attribute.Name || a.Slug == attribute.Slug))
            throw new InvalidOperationException("An attribute with this name already exists.");

        attribute.Id = Guid.NewGuid();
        attribute.CreatedAt = DateTimeOffset.UtcNow;
        _db.AttributeDefinitions.Add(attribute);
        await _db.SaveChangesAsync();
        return attribute;
    }

    public async Task<AttributeDefinition> UpdateAsync(Guid id, AttributeDefinition attribute, byte[] expectedVersion)
    {
        var existing = await _db.AttributeDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id)
            ?? throw new InvalidOperationException("Attribute not found");

        if (!existing.RowVersion.AsSpan().SequenceEqual(expectedVersion))
            throw new ConcurrencyConflictException(EntityName);

        if (existing.IsBuiltIn && existing.DataType != attribute.DataType)
            throw new InvalidOperationException(BuiltInProtectedMessage);

        var slug = ToSlug(attribute.Name);
        if (await _db.AttributeDefinitions.AnyAsync(a => a.Id != id && (a.Name == attribute.Name || a.Slug == slug)))
            throw new InvalidOperationException("An attribute with this name already exists.");

        existing.Name = attribute.Name;
        existing.Slug = slug;
        existing.CategoryId = attribute.CategoryId;
        existing.DataType = attribute.DataType;
        existing.Description = attribute.Description;
        existing.IsRequired = attribute.IsRequired;
        existing.SortOrder = attribute.SortOrder;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

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

    public async Task DeleteAsync(Guid id)
    {
        var attribute = await _db.AttributeDefinitions.FindAsync(id)
            ?? throw new InvalidOperationException("Attribute not found");

        if (attribute.IsBuiltIn)
            throw new InvalidOperationException(BuiltInProtectedMessage);

        _db.AttributeDefinitions.Remove(attribute);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteManyAsync(IReadOnlyCollection<Guid> ids)
    {
        if (ids.Count == 0)
            return;

        var attributes = await _db.AttributeDefinitions
            .Where(a => ids.Contains(a.Id))
            .ToListAsync();

        if (attributes.Any(a => a.IsBuiltIn))
            throw new InvalidOperationException(BuiltInProtectedMessage);

        _db.AttributeDefinitions.RemoveRange(attributes);
        await _db.SaveChangesAsync();
    }

    public async Task<List<AttributeDefinition>> GetRecentlyUsedAsync(int count)
    {
        return await _db.PositionAttributeRules
            .AsNoTracking()
            .OrderByDescending(r => r.Position!.CreatedAt)
            .Select(r => r.AttributeDefinition)
            .Distinct()
            .Take(count)
            .ToListAsync();
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

    private static string ToSlug(string name) => AttributeSlug.FromName(name);
}

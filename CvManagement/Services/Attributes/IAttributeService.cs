using CvManagement.Data.Entities.Attributes;

namespace CvManagement.Services.Attributes;

public interface IAttributeService
{
    Task<List<AttributeDefinition>> GetAllAsync();

    /// <summary>The fixed category lookup list, for grouping and filtering in the UI only.</summary>
    Task<List<AttributeCategory>> GetCategoriesAsync();
    Task<AttributeDefinition?> GetByIdAsync(Guid id);
    Task<AttributeDefinition> CreateAsync(AttributeDefinition attribute);
    Task<AttributeDefinition> UpdateAsync(Guid id, AttributeDefinition attribute, byte[] expectedVersion);
    Task DeleteAsync(Guid id);

    /// <summary>Deletes every non-built-in attribute in <paramref name="ids"/> in one save.</summary>
    Task DeleteManyAsync(IReadOnlyCollection<Guid> ids);
    Task<List<AttributeOption>> GetOptionsAsync(Guid attributeId);
    Task<AttributeOption> AddOptionAsync(Guid attributeId, string label);
    Task RemoveOptionAsync(Guid attributeId, Guid optionId);
    Task<List<AttributeDefinition>> GetRecentlyUsedAsync(int count);
}

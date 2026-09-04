using CvManagement.Data.Entities.Attributes;

namespace CvManagement.Services.Attributes;

public interface IAttributeService
{
    Task<List<AttributeDefinition>> GetAllAsync();
    Task<List<AttributeDefinition>> GetByCategoryAsync(AttributeCategory category);
    Task<AttributeDefinition?> GetByIdAsync(Guid id);
    Task<AttributeDefinition> CreateAsync(AttributeDefinition attribute);
    Task<AttributeDefinition> UpdateAsync(Guid id, AttributeDefinition attribute);
    Task DeleteAsync(Guid id);
    Task<List<AttributeOption>> GetOptionsAsync(Guid attributeId);
    Task<AttributeOption> AddOptionAsync(Guid attributeId, string label);
    Task RemoveOptionAsync(Guid attributeId, Guid optionId);
    Task<List<AttributeDefinition>> SearchByPrefixAsync(string prefix);
}

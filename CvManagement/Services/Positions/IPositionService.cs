using CvManagement.Data.Entities.Positions;
using CvManagement.Data.Entities.Attributes;

namespace CvManagement.Services.Positions;

public interface IPositionService
{
    Task<List<Position>> GetAllAsync(bool? publicOnly = null);
    Task<Position?> GetByIdAsync(Guid id);
    Task<Position> CreateAsync(Position position, Guid createdByUserId);
    Task<Position> UpdateAsync(Guid id, Position position);
    Task DeleteAsync(Guid id);
    Task<Position> DuplicateAsync(Guid sourceId, Guid createdByUserId);
    Task AddAttributeRuleAsync(Guid positionId, Guid attributeId, bool isRequired);
    Task RemoveAttributeRuleAsync(Guid positionId, Guid attributeId);
    Task AddAccessRuleAsync(Guid positionId, Guid attributeId, PositionAccessOperator op, string filterValue);
    Task RemoveAccessRuleAsync(Guid positionId, Guid ruleId);
    Task AddTagAsync(Guid positionId, string tag);
    Task RemoveTagAsync(Guid positionId, string tag);
    Task<List<Position>> GetAccessiblePositionsAsync(Guid userId);
    Task<int> GetCvCountAsync(Guid positionId);
}

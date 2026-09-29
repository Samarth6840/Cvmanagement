using CvManagement.Data.Entities.Positions;
using CvManagement.Data.Entities.Attributes;

namespace CvManagement.Services.Positions;

public interface IPositionService
{
    Task<List<Position>> GetAllAsync(bool? publicOnly = null);
    Task<Position?> GetByIdAsync(Guid id);
    Task<Position> CreateAsync(Position position, Guid createdByUserId);
    Task<Position> UpdateAsync(Guid id, Position position, byte[] expectedVersion);
    Task DeleteAsync(Guid id);
    Task DeleteManyAsync(IReadOnlyCollection<Guid> ids);
    Task SetTagsAsync(Guid positionId, IReadOnlyCollection<string> tags);
    Task<Position> DuplicateAsync(Guid id, Guid createdByUserId);
    Task<bool> CanCandidateAccessAsync(Guid candidateProfileId, Guid positionId);
    Task<List<Position>> GetAccessiblePositionsAsync(Guid candidateProfileId);
    Task<List<Position>> GetLatestAsync(int count);
    Task<List<Position>> GetMostPopularAsync(int count);
    Task<List<(string Tag, int Count)>> GetTagCountsAsync();
    Task<List<string>> GetAllTagNamesAsync();
    Task<(int Positions, int Candidates, int Recruiters, int Cvs24h, int TotalCvs)> GetLandingStatsAsync();
}

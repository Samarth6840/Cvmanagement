using CvManagement.Data;
using CvManagement.Data.Entities.Profiles;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Services.Profiles;

public interface IProfileService
{
    Task<CandidateProfile> GetOrCreateProfileAsync(Guid userId);
    Task<CandidateProfile?> GetByUserIdAsync(Guid userId);
}

public class ProfileService : IProfileService
{
    private readonly CvDbContext _db;

    public ProfileService(CvDbContext db) => _db = db;

    public async Task<CandidateProfile?> GetByUserIdAsync(Guid userId) =>
        await _db.CandidateProfiles
            .Include(p => p.User)
            .Include(p => p.AttributeValues)
                .ThenInclude(v => v.AttributeDefinition)
            .Include(p => p.AttributeValues)
                .ThenInclude(v => v.SelectedOption)
            .FirstOrDefaultAsync(p => p.UserId == userId);

    public async Task<CandidateProfile> GetOrCreateProfileAsync(Guid userId)
    {
        var existing = await _db.CandidateProfiles
            .Include(p => p.AttributeValues)
                .ThenInclude(v => v.AttributeDefinition)
            .Include(p => p.AttributeValues)
                .ThenInclude(v => v.SelectedOption)
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (existing is not null) return existing;

        var profile = new CandidateProfile
        {
            UserId = userId
        };
        _db.CandidateProfiles.Add(profile);
        await _db.SaveChangesAsync();
        return profile;
    }
}

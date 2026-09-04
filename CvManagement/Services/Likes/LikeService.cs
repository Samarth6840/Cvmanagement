using CvManagement.Data.Entities.Cv;
using CvManagement.Data.Entities.Likes;
using CvManagement.Data.Entities.Profiles;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Services.Likes;

public interface ILikeService
{
    Task<bool> ToggleAsync(Guid userId, Guid cvRecordId);
    Task<int> GetLikeCountAsync(Guid cvRecordId);
    Task<bool> HasUserLikedAsync(Guid userId, Guid cvRecordId);
    Task<List<CvRecord>> GetTopLikedAsync(int count);
}

public class LikeService : ILikeService
{
    private readonly Data.CvDbContext _db;

    public LikeService(Data.CvDbContext db) => _db = db;

    public async Task<bool> ToggleAsync(Guid userId, Guid cvRecordId)
    {
        var existing = await _db.CvLikes
            .FirstOrDefaultAsync(l => l.UserId == userId && l.CvRecordId == cvRecordId);

        if (existing is not null)
        {
            _db.CvLikes.Remove(existing);
            await _db.SaveChangesAsync();
            return false;
        }
        else
        {
            _db.CvLikes.Add(new CvLike { UserId = userId, CvRecordId = cvRecordId });
            await _db.SaveChangesAsync();
            return true;
        }
    }

    public async Task<int> GetLikeCountAsync(Guid cvRecordId) =>
        await _db.CvLikes.CountAsync(l => l.CvRecordId == cvRecordId);

    public async Task<bool> HasUserLikedAsync(Guid userId, Guid cvRecordId) =>
        await _db.CvLikes.AnyAsync(l => l.UserId == userId && l.CvRecordId == cvRecordId);

    public async Task<List<CvRecord>> GetTopLikedAsync(int count) =>
        await _db.CvRecords
            .Include(c => c.CandidateProfile).ThenInclude(p => p!.User)
            .Include(c => c.Position)
            .OrderByDescending(c => c.LikeCount)
            .Take(count)
            .ToListAsync();
}

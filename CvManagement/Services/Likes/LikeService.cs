using CvManagement.Data.Entities.Cv;
using CvManagement.Data.Entities.Likes;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Services.Likes;

public interface ILikeService
{
    Task<bool> ToggleAsync(Guid userId, Guid cvRecordId);
    Task<int> GetLikeCountAsync(Guid cvRecordId);
    Task<bool> HasUserLikedAsync(Guid userId, Guid cvRecordId);
}

public class LikeService : ILikeService
{
    private readonly Data.CvDbContext _db;

    public LikeService(Data.CvDbContext db) => _db = db;

    public async Task<bool> ToggleAsync(Guid userId, Guid cvRecordId)
    {
        var existing = await _db.CvLikes
            .FirstOrDefaultAsync(l => l.UserId == userId && l.CvRecordId == cvRecordId);

        bool liked;
        if (existing is not null)
        {
            _db.CvLikes.Remove(existing);
            liked = false;
        }
        else
        {
            _db.CvLikes.Add(new CvLike { UserId = userId, CvRecordId = cvRecordId });
            liked = true;
        }

        var cv = await _db.CvRecords.FindAsync(cvRecordId);
        if (cv is not null)
            cv.LikeCount = Math.Max(0, cv.LikeCount + (liked ? 1 : -1));

        await _db.SaveChangesAsync();
        return liked;
    }

    public async Task<int> GetLikeCountAsync(Guid cvRecordId) =>
        await _db.CvLikes.CountAsync(l => l.CvRecordId == cvRecordId);

    public async Task<bool> HasUserLikedAsync(Guid userId, Guid cvRecordId) =>
        await _db.CvLikes.AnyAsync(l => l.UserId == userId && l.CvRecordId == cvRecordId);
}

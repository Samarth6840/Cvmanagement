using CvManagement.Data.Entities.Cv;
using CvManagement.Data.Entities.Identity;
using CvManagement.Data.Entities.Likes;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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
    private readonly Auth.ICurrentUserService _currentUser;

    public LikeService(Data.CvDbContext db, Auth.ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    // Spec §9: recruiters only. Enforced here, not just by hiding the button, because
    // the endpoint is otherwise reachable by any authenticated user.
    public async Task<bool> ToggleAsync(Guid userId, Guid cvRecordId)
    {
        if (!await _currentUser.IsInRoleAsync(RoleNames.Recruiter)
            && !await _currentUser.IsInRoleAsync(RoleNames.Administrator))
            throw new UnauthorizedAccessException("Only recruiters can like CVs.");

        if (await _currentUser.GetUserIdAsync() != userId)
            throw new UnauthorizedAccessException("Cannot like as another user.");

        var cvExists = await _db.CvRecords
            .Where(c => c.Id == cvRecordId)
            .Select(c => c.Status)
            .FirstOrDefaultAsync();
        if (cvExists != CvStatus.Published)
            throw new InvalidOperationException("Only published CVs can be liked.");

        var existing = await _db.CvLikes
            .FirstOrDefaultAsync(l => l.UserId == userId && l.CvRecordId == cvRecordId);

        if (existing is not null)
        {
            _db.CvLikes.Remove(existing);
        }
        else
        {
            _db.CvLikes.Add(new CvLike { UserId = userId, CvRecordId = cvRecordId });
        }

        await SaveAsync(cvRecordId);
        return existing is null;
    }

    // Recount rather than read-modify-write on the denormalized counter: two recruiters
    // liking at once previously raced, and the concurrency failure rolled back the like too.
    private async Task SaveAsync(Guid cvRecordId)
    {
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new InvalidOperationException("That like was already recorded.");
        }

        var count = await _db.CvLikes.CountAsync(l => l.CvRecordId == cvRecordId);
        await _db.CvRecords
            .Where(c => c.Id == cvRecordId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.LikeCount, count));
    }

    public async Task<int> GetLikeCountAsync(Guid cvRecordId) =>
        await _db.CvLikes.CountAsync(l => l.CvRecordId == cvRecordId);

    public async Task<bool> HasUserLikedAsync(Guid userId, Guid cvRecordId) =>
        await _db.CvLikes.AnyAsync(l => l.UserId == userId && l.CvRecordId == cvRecordId);
}

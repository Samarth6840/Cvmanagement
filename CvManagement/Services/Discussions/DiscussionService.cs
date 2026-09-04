using CvManagement.Data.Entities.Discussions;
using CvManagement.Data.Entities.Identity;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Services.Discussions;

public interface IDiscussionService
{
    Task<List<DiscussionMessage>> GetMessagesAsync(Guid positionId, int page = 1, int pageSize = 50);
    Task<DiscussionMessage> AddMessageAsync(Guid positionId, Guid authorUserId, string content);
}

public class DiscussionService : IDiscussionService
{
    private readonly Data.CvDbContext _db;

    public DiscussionService(Data.CvDbContext db) => _db = db;

    public async Task<List<DiscussionMessage>> GetMessagesAsync(Guid positionId, int page = 1, int pageSize = 50) =>
        await _db.DiscussionMessages
            .Include(m => m.User)
            .Where(m => m.PositionId == positionId)
            .OrderBy(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

    public async Task<DiscussionMessage> AddMessageAsync(Guid positionId, Guid authorUserId, string content)
    {
        var message = new DiscussionMessage
        {
            PositionId = positionId,
            UserId = authorUserId,
            Content = content
        };
        _db.DiscussionMessages.Add(message);
        await _db.SaveChangesAsync();

        message.User = await _db.Users.FindAsync(authorUserId);
        return message;
    }
}

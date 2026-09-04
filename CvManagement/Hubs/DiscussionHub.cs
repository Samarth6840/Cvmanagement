using CvManagement.Services.Discussions;
using CvManagement.Services.Auth;
using Microsoft.AspNetCore.SignalR;

namespace CvManagement.Hubs;

public class DiscussionHub : Hub
{
    private readonly IDiscussionService _discussionService;
    private readonly ICurrentUserService _currentUserService;

    public DiscussionHub(IDiscussionService discussionService, ICurrentUserService currentUserService)
    {
        _discussionService = discussionService;
        _currentUserService = currentUserService;
    }

    public async Task JoinGroup(Guid positionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, positionId.ToString());
    }

    public async Task SendMessage(Guid positionId, string content)
    {
        var userId = await _currentUserService.GetUserIdAsync();
        if (userId is null) return;

        var message = await _discussionService.AddMessageAsync(positionId, userId.Value, content);
        await Clients.Group(positionId.ToString()).SendAsync("ReceiveMessage", message);
    }
}

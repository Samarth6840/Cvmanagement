using CvManagement.Data.Entities.Identity;
using CvManagement.Services.Auth;
using CvManagement.Services.Discussions;
using CvManagement.Services.Positions;
using CvManagement.Services.Profiles;
using Microsoft.AspNetCore.SignalR;

namespace CvManagement.Hubs;

public class DiscussionHub : Hub
{
    private readonly IDiscussionService _discussionService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPositionService _positionService;
    private readonly IProfileService _profileService;

    public DiscussionHub(
        IDiscussionService discussionService,
        ICurrentUserService currentUserService,
        IPositionService positionService,
        IProfileService profileService)
    {
        _discussionService = discussionService;
        _currentUserService = currentUserService;
        _positionService = positionService;
        _profileService = profileService;
    }

    public async Task JoinGroup(Guid positionId)
    {
        if (!await CanAccessPositionAsync(positionId)) return;
        await Groups.AddToGroupAsync(Context.ConnectionId, positionId.ToString());
    }

    public async Task SendMessage(Guid positionId, string content)
    {
        var userId = await _currentUserService.GetUserIdAsync();
        if (userId is null) return;
        if (!await CanAccessPositionAsync(positionId)) return;

        var message = await _discussionService.AddMessageAsync(positionId, userId.Value, content);
        await Clients.Group(positionId.ToString()).SendAsync("ReceiveMessage", message);
    }

    private async Task<bool> CanAccessPositionAsync(Guid positionId)
    {
        if (await _currentUserService.IsInRoleAsync(RoleNames.Recruiter)
            || await _currentUserService.IsInRoleAsync(RoleNames.Administrator))
            return true;

        var userId = await _currentUserService.GetUserIdAsync();
        if (userId is null) return false;

        var profile = await _profileService.GetByUserIdAsync(userId.Value);
        return profile is not null
            && await _positionService.CanCandidateAccessAsync(profile.Id, positionId);
    }
}

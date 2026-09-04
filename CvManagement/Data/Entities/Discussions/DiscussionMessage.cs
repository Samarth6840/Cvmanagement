namespace CvManagement.Data.Entities.Discussions;

public class DiscussionMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PositionId { get; set; }

    public Guid UserId { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Position Position { get; set; } = null!;

    public ApplicationUser User { get; set; } = null!;
}

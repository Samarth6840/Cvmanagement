using System.ComponentModel.DataAnnotations;

namespace CvManagement.Data.Entities.Positions;

public class PositionTag
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PositionId { get; set; }

    [MaxLength(64)]
    public string Tag { get; set; } = string.Empty;

    public Position Position { get; set; } = null!;
}

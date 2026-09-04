namespace CvManagement.Data;

public interface IAuditable
{
    DateTimeOffset? UpdatedAt { get; set; }
}

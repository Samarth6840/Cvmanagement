namespace CvManagement.Services.Concurrency;

public class ConcurrencyConflictException(string entityName)
    : Exception($"{entityName} was modified by someone else. Reload to see the current values, then reapply your changes.")
{
    public string EntityName { get; } = entityName;
}

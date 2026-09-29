namespace CvManagement.Services.Concurrency;

/// <summary>
/// Thrown when an edit is rejected because the stored version no longer matches the
/// version the client read. Surfaces as a reload-and-retry prompt, never as a silent overwrite.
/// </summary>
public class ConcurrencyConflictException(string entityName)
    : Exception($"{entityName} was modified by someone else. Reload to see the current values, then reapply your changes.")
{
    public string EntityName { get; } = entityName;
}

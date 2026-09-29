namespace CvManagement.Data.Entities.Attributes;

/// <summary>
/// One place that turns an attribute name into its stable slug, shared by the service that
/// creates attributes and the seeder that ships the built-in ones.
/// </summary>
public static class AttributeSlug
{
    public static string FromName(string name) =>
        string.Join("-", name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.ToLowerInvariant()));
}

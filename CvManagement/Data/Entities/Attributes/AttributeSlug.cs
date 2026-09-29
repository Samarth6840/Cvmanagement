namespace CvManagement.Data.Entities.Attributes;

public static class AttributeSlug
{
    public static string FromName(string name) =>
        string.Join("-", name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.ToLowerInvariant()));
}

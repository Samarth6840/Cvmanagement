using CvManagement.Data.Entities.Attributes;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Data.Seed;

/// <summary>
/// Ships the built-in "Me" attributes (spec §5.1). They are ordinary library attributes —
/// same table, same engine, attachable to any position template — but flagged
/// <see cref="AttributeDefinition.IsBuiltIn"/> so they can never be deleted.
/// </summary>
public static class AttributeSeed
{
    private static readonly AttributeDefinition[] BuiltInAttributes =
    [
        new() { Name = "First Name", CategoryId = AttributeCategoryCatalog.PersonalInformation, DataType = AttributeDataType.String, SortOrder = 0 },
        new() { Name = "Last Name", CategoryId = AttributeCategoryCatalog.PersonalInformation, DataType = AttributeDataType.String, SortOrder = 1 },
        new() { Name = "Location", CategoryId = AttributeCategoryCatalog.PersonalInformation, DataType = AttributeDataType.String, SortOrder = 2 },
        new() { Name = "Personal Photo", CategoryId = AttributeCategoryCatalog.PersonalInformation, DataType = AttributeDataType.Image, SortOrder = 3 }
    ];

    /// <summary>
    /// Runs on every startup. An attribute already carrying one of the built-in names is
    /// promoted rather than duplicated, so the unique name index is never violated and a
    /// pre-existing value is preserved.
    /// </summary>
    public static async Task SeedBuiltInAttributesAsync(IServiceProvider serviceProvider)
    {
        var db = serviceProvider.GetRequiredService<CvDbContext>();

        var existing = await db.AttributeDefinitions.ToListAsync();

        foreach (var template in BuiltInAttributes)
        {
            var match = existing.FirstOrDefault(a =>
                string.Equals(a.Name, template.Name, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                if (!match.IsBuiltIn)
                    match.IsBuiltIn = true;

                continue;
            }

            template.Id = Guid.NewGuid();
            template.Slug = AttributeSlug.FromName(template.Name);
            template.Description = "Built-in profile attribute. Present on every profile and protected from deletion.";
            template.CreatedAt = DateTimeOffset.UtcNow;
            template.IsBuiltIn = true;
            db.AttributeDefinitions.Add(template);
        }

        await db.SaveChangesAsync();
    }
}

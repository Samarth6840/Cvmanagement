using CvManagement.Data.Entities.Attributes;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Data.Seed;

/// <summary>
/// Ensures the fixed category lookup list exists. The migration that introduced the table also
/// inserts these rows; this runs on every startup so a database where a row was removed by hand
/// is repaired rather than left with attributes pointing at a missing category.
/// </summary>
public static class AttributeCategorySeed
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        var db = serviceProvider.GetRequiredService<CvDbContext>();

        var existingIds = await db.AttributeCategories.Select(c => c.Id).ToListAsync();
        var missing = AttributeCategoryCatalog.All
            .Where(category => !existingIds.Contains(category.Id))
            .ToList();

        if (missing.Count == 0)
            return;

        foreach (var (id, name) in missing)
        {
            db.AttributeCategories.Add(new AttributeCategory
            {
                Id = id,
                Name = name,
                SortOrder = id
            });
        }

        await db.SaveChangesAsync();
    }
}

using CvManagement.Data.Entities.Attributes;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Data.Seed;

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

using Microsoft.EntityFrameworkCore;

namespace CvManagement.Services.Search;

public interface ISearchService
{
    Task<List<SearchResult>> SearchAsync(string query);
}

public class SearchResult
{
    public string Type { get; set; } = "";
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string? Subtitle { get; set; }
    public double Rank { get; set; }
}

public class SearchService : ISearchService
{
    private readonly Data.CvDbContext _db;

    public SearchService(Data.CvDbContext db) => _db = db;

    public async Task<List<SearchResult>> SearchAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new();

        var positionResults = await _db.Database
            .SqlQuery<SearchResult>($@"
                SELECT 'Position' AS ""Type"", p.""Id"", p.""Title"", p.""Company"" AS ""Subtitle"",
                       ts_rank(p.""SearchVector"", plainto_tsquery('english', {query})) AS ""Rank""
                FROM ""Positions"" p
                WHERE p.""SearchVector"" @@ plainto_tsquery('english', {query})
                ORDER BY ""Rank"" DESC
                LIMIT 20")
            .ToListAsync();

        var cvResults = await _db.Database
            .SqlQuery<SearchResult>($@"
                SELECT 'CV' AS ""Type"", c.""Id"", c.""Title"", u.""DisplayName"" AS ""Subtitle"",
                       ts_rank(c.""SearchVector"", plainto_tsquery('english', {query})) AS ""Rank""
                FROM ""CvRecords"" c
                JOIN ""CandidateProfiles"" cp ON c.""CandidateProfileId"" = cp.""Id""
                JOIN ""Users"" u ON cp.""UserId"" = u.""Id""
                WHERE c.""SearchVector"" @@ plainto_tsquery('english', {query})
                ORDER BY ""Rank"" DESC
                LIMIT 20")
            .ToListAsync();

        return positionResults.Concat(cvResults).ToList();
    }
}

using CvManagement.Data.Entities.Cv;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;

namespace CvManagement.Services.Search;

public interface ISearchService
{
    Task<List<SearchResult>> SearchAsync(string query);
    Task<List<SearchResult>> SearchAsync(string query, bool isRecruiterOrAdmin);
}

public enum SearchResultType
{
    Position,
    Cv
}

public class SearchResult
{
    public SearchResultType Type { get; set; }
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public double Rank { get; set; }
}

public class SearchService : ISearchService
{
    private const int PositionResultLimit = 20;
    private const int CvResultLimit = 50;

    private readonly Data.CvDbContext _db;

    public SearchService(Data.CvDbContext db) => _db = db;

    public Task<List<SearchResult>> SearchAsync(string query) =>
        SearchAsync(query, isRecruiterOrAdmin: true);

    public async Task<List<SearchResult>> SearchAsync(string query, bool isRecruiterOrAdmin)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var positionResults = await SearchPositionsAsync(query, isRecruiterOrAdmin);

        // CV content belongs to candidates, not to other candidates: only recruiters and
        // administrators may search it. Access rules gate what a *candidate* may see of a
        // position, so they do not apply to a recruiter's own search.
        if (!isRecruiterOrAdmin)
            return positionResults;

        var cvResults = await SearchCvsAsync(query);
        return positionResults.Concat(cvResults.OrderByDescending(r => r.Rank)).ToList();
    }

    private async Task<List<SearchResult>> SearchPositionsAsync(string query, bool isRecruiterOrAdmin)
    {
        // Restricted positions are recruiter-only, so a candidate or anonymous search must
        // not see them even when the text matches.
        //
        // The flag is passed as a boolean *parameter*. Building this predicate as an
        // interpolated string variable would send it to PostgreSQL as a text parameter and
        // fail with "argument of AND must be type boolean, not type text".
        return await _db.Database
            .SqlQuery<SearchResult>($@"
                SELECT {(int)SearchResultType.Position} AS ""Type"", p.""Id"", p.""Title"",
                       p.""Company"" AS ""Subtitle"",
                       ts_rank(p.""SearchVector"", plainto_tsquery('english', {query})) AS ""Rank""
                FROM ""Positions"" p
                WHERE p.""SearchVector"" @@ plainto_tsquery('english', {query})
                  AND ({isRecruiterOrAdmin} OR p.""IsPublic"" = TRUE)
                ORDER BY ""Rank"" DESC
                LIMIT {PositionResultLimit}")
            .ToListAsync();
    }

    // CV *content* is EAV, so it is matched through ProfileAttributeValues.SearchVector
    // rather than the CvRecords trigger, which only sees titles and the candidate name.
    private async Task<List<SearchResult>> SearchCvsAsync(string query) =>
        await _db.Database
            .SqlQuery<SearchResult>($@"
                WITH ""matching_profiles"" AS (
                    SELECT DISTINCT v.""CandidateProfileId""
                    FROM ""ProfileAttributeValues"" v
                    WHERE v.""SearchVector"" @@ plainto_tsquery('english', {query})
                ),
                ""scored"" AS (
                    SELECT c.""Id"" AS ""CvId"",
                           max(ts_rank(v.""SearchVector"", plainto_tsquery('english', {query}))) AS ""ContentRank"",
                           c.""SearchVector"" AS ""MetaVector""
                    FROM ""CvRecords"" c
                    JOIN ""matching_profiles"" mp ON mp.""CandidateProfileId"" = c.""CandidateProfileId""
                    LEFT JOIN ""ProfileAttributeValues"" v
                           ON v.""CandidateProfileId"" = c.""CandidateProfileId""
                          AND v.""SearchVector"" @@ plainto_tsquery('english', {query})
                    WHERE c.""Status"" = {(int)CvStatus.Published}
                    GROUP BY c.""Id"", c.""SearchVector""
                )
                SELECT {(int)SearchResultType.Cv} AS ""Type"", s.""CvId"" AS ""Id"",
                       c.""Title"" AS ""Title"", u.""DisplayName"" AS ""Subtitle"",
                       (s.""ContentRank"" + ts_rank(s.""MetaVector"", plainto_tsquery('english', {query}))) AS ""Rank""
                FROM ""scored"" s
                JOIN ""CvRecords"" c ON c.""Id"" = s.""CvId""
                JOIN ""CandidateProfiles"" cp ON c.""CandidateProfileId"" = cp.""Id""
                 JOIN ""Users"" u ON cp.""UserId"" = u.""Id""
                 ORDER BY ""Rank"" DESC
                 LIMIT {CvResultLimit}")
            .ToListAsync();
}

using CvManagement.Data;
using CvManagement.Data.Entities.Projects;
using CvManagement.Services.Concurrency;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Services.Projects;

// Spec §5.3 / §3.5: projects belong to a candidate's own profile. Listing, editing and
// deleting are all scoped to the owning profile, so one candidate can never touch another
// candidate's projects — and a Recruiter's read-only CV view is not a write path.
public interface IProjectService
{
    Task<List<Project>> GetForCandidateAsync(Guid candidateProfileId);
    Task<Project?> GetForCandidateAsync(Guid candidateProfileId, Guid projectId);
    Task<Project> CreateForCandidateAsync(Guid candidateProfileId, Project project, IReadOnlyCollection<string> tags);
    Task<Project> UpdateForCandidateAsync(
        Guid candidateProfileId,
        Guid projectId,
        Project project,
        IReadOnlyCollection<string> tags,
        byte[] expectedVersion);
    Task DeleteForCandidateAsync(Guid candidateProfileId, Guid projectId);
    Task DeleteManyForCandidateAsync(Guid candidateProfileId, IReadOnlyCollection<Guid> projectIds);
    Task<List<string>> GetAllTagNamesAsync();
}

public class ProjectService : IProjectService
{
    private const string EntityName = "This project";
    private const string NotOwnedMessage = "You can only manage your own projects.";

    private readonly CvDbContext _db;

    public ProjectService(CvDbContext db) => _db = db;

    public Task<List<Project>> GetForCandidateAsync(Guid candidateProfileId) =>
        _db.CandidateProjects
            .AsNoTracking()
            .Where(cp => cp.CandidateProfileId == candidateProfileId)
            .Select(cp => cp.Project)
            .Include(p => p.Tags)
            .OrderByDescending(p => p.StartDate)
            .ThenByDescending(p => p.CreatedAt)
            .ToListAsync();

    public Task<Project?> GetForCandidateAsync(Guid candidateProfileId, Guid projectId) =>
        _db.CandidateProjects
            .AsNoTracking()
            .Where(cp => cp.CandidateProfileId == candidateProfileId && cp.ProjectId == projectId)
            .Select(cp => cp.Project)
            .Include(p => p.Tags)
            .FirstOrDefaultAsync();

    public async Task<Project> CreateForCandidateAsync(
        Guid candidateProfileId,
        Project project,
        IReadOnlyCollection<string> tags)
    {
        project.Id = Guid.NewGuid();
        project.CreatedAt = DateTimeOffset.UtcNow;
        project.Tags = tags.Select(tag => new ProjectTag { Tag = tag }).ToList();

        // The link is what makes the project the candidate's own; it is written together
        // with the project in one save so a project can never exist unowned.
        _db.Projects.Add(project);
        _db.CandidateProjects.Add(new CandidateProject
        {
            CandidateProfileId = candidateProfileId,
            ProjectId = project.Id
        });

        await _db.SaveChangesAsync();
        return project;
    }

    // Optimistic locking (spec §4).
    public async Task<Project> UpdateForCandidateAsync(
        Guid candidateProfileId,
        Guid projectId,
        Project project,
        IReadOnlyCollection<string> tags,
        byte[] expectedVersion)
    {
        var existing = await _db.Projects
            .Include(p => p.Tags)
            .FirstOrDefaultAsync(p => p.Id == projectId)
            ?? throw new InvalidOperationException("Project not found");

        await EnsureOwnedAsync(candidateProfileId, projectId);

        if (!existing.RowVersion.AsSpan().SequenceEqual(expectedVersion))
            throw new ConcurrencyConflictException(EntityName);

        existing.Name = project.Name;
        existing.Description = project.Description;
        existing.Url = project.Url;
        existing.StartDate = project.StartDate;
        existing.EndDate = project.EndDate;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        ReplaceTags(existing, tags);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException(EntityName);
        }

        return existing;
    }

    // Removes the candidate's own link. The project row itself is only deleted when nobody
    // else references it, so a shared project is not pulled out from under another profile.
    public async Task DeleteForCandidateAsync(Guid candidateProfileId, Guid projectId)
    {
        await EnsureOwnedAsync(candidateProfileId, projectId);

        var link = await _db.CandidateProjects
            .FirstAsync(cp => cp.CandidateProfileId == candidateProfileId && cp.ProjectId == projectId);
        _db.CandidateProjects.Remove(link);

        var stillReferenced = await _db.CandidateProjects
            .AnyAsync(cp => cp.ProjectId == projectId && cp.Id != link.Id);

        if (!stillReferenced)
        {
            var project = await _db.Projects.FindAsync(projectId);
            if (project is not null)
                _db.Projects.Remove(project);
        }

        await _db.SaveChangesAsync();
    }

    // One load and one save for a multi-row toolbar delete. Projects another candidate still
    // references are kept; only their link is removed.
    public async Task DeleteManyForCandidateAsync(Guid candidateProfileId, IReadOnlyCollection<Guid> projectIds)
    {
        if (projectIds.Count == 0)
            return;

        var ownedLinks = await _db.CandidateProjects
            .Where(cp => cp.CandidateProfileId == candidateProfileId && projectIds.Contains(cp.ProjectId))
            .ToListAsync();

        _db.CandidateProjects.RemoveRange(ownedLinks);

        // Any other link to these projects means the project itself must survive.
        var stillLinkedProjectIds = await _db.CandidateProjects
            .Where(cp => projectIds.Contains(cp.ProjectId)
                         && cp.CandidateProfileId != candidateProfileId)
            .Select(cp => cp.ProjectId)
            .Distinct()
            .ToListAsync();

        var orphans = await _db.Projects
            .Where(p => projectIds.Contains(p.Id) && !stillLinkedProjectIds.Contains(p.Id))
            .ToListAsync();

        _db.Projects.RemoveRange(orphans);
        await _db.SaveChangesAsync();
    }

    // Feeds the tag-input autocomplete (spec §5.3): tags already entered anywhere in the system.
    public async Task<List<string>> GetAllTagNamesAsync() =>
        await _db.ProjectTags
            .AsNoTracking()
            .Select(t => t.Tag)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync();

    private async Task EnsureOwnedAsync(Guid candidateProfileId, Guid projectId)
    {
        var owned = await _db.CandidateProjects
            .AnyAsync(cp => cp.CandidateProfileId == candidateProfileId && cp.ProjectId == projectId);
        if (!owned)
            throw new UnauthorizedAccessException(NotOwnedMessage);
    }

    private static void ReplaceTags(Project project, IReadOnlyCollection<string> tags)
    {
        var desired = tags.Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Removing in place keeps the whole change set in a single SaveChanges call.
        foreach (var stale in project.Tags
                     .Where(t => !desired.Contains(t.Tag, StringComparer.OrdinalIgnoreCase))
                     .ToList())
        {
            project.Tags.Remove(stale);
        }

        var current = project.Tags.Select(t => t.Tag).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in desired.Where(tag => !current.Contains(tag)))
            project.Tags.Add(new ProjectTag { ProjectId = project.Id, Tag = tag });
    }
}

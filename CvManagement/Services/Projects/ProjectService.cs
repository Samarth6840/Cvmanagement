using CvManagement.Data;
using CvManagement.Data.Entities.Projects;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Services.Projects;

public interface IProjectService
{
    Task<List<Project>> GetAllAsync();
    Task<Project?> GetByIdAsync(Guid id);
    Task<Project> CreateAsync(Project project);
    Task<Project> UpdateAsync(Guid id, Project project);
    Task DeleteAsync(Guid id);
    Task AddTagAsync(Guid projectId, string tag);
    Task RemoveTagAsync(Guid projectId, string tag);
    Task<List<string>> GetAllTagsAsync();
}

public class ProjectService : IProjectService
{
    private readonly CvDbContext _db;

    public ProjectService(CvDbContext db) => _db = db;

    public async Task<List<Project>> GetAllAsync() =>
        await _db.Projects
            .Include(p => p.Tags)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

    public async Task<Project?> GetByIdAsync(Guid id) =>
        await _db.Projects
            .Include(p => p.Tags)
            .FirstOrDefaultAsync(p => p.Id == id);

    public async Task<Project> CreateAsync(Project project)
    {
        project.Id = Guid.NewGuid();
        project.CreatedAt = DateTimeOffset.UtcNow;
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();
        return project;
    }

    public async Task<Project> UpdateAsync(Guid id, Project project)
    {
        var existing = await _db.Projects.FindAsync(id)
            ?? throw new InvalidOperationException("Project not found");

        existing.Name = project.Name;
        existing.Description = project.Description;
        existing.Url = project.Url;
        existing.StartDate = project.StartDate;
        existing.EndDate = project.EndDate;

        await _db.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAsync(Guid id)
    {
        var project = await _db.Projects.FindAsync(id)
            ?? throw new InvalidOperationException("Project not found");
        _db.Projects.Remove(project);
        await _db.SaveChangesAsync();
    }

    public async Task AddTagAsync(Guid projectId, string tag)
    {
        var exists = await _db.ProjectTags
            .AnyAsync(t => t.ProjectId == projectId && t.Tag == tag);
        if (exists) return;

        _db.ProjectTags.Add(new ProjectTag { ProjectId = projectId, Tag = tag });
        await _db.SaveChangesAsync();
    }

    public async Task RemoveTagAsync(Guid projectId, string tag)
    {
        var tagEntity = await _db.ProjectTags
            .FirstOrDefaultAsync(t => t.ProjectId == projectId && t.Tag == tag);
        if (tagEntity is not null)
        {
            _db.ProjectTags.Remove(tagEntity);
            await _db.SaveChangesAsync();
        }
    }

    public async Task<List<string>> GetAllTagsAsync() =>
        await _db.ProjectTags
            .Select(t => t.Tag)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync();
}

using Microsoft.EntityFrameworkCore;

namespace TaskManagement;

public class ProjectService(Context dbContext)
{
    private readonly Context _dbContext = dbContext;

  // CREATE
  public async Task<ProjectModel> CreateProjectAsync(
        Guid tenantId,
        string name,
        string keyPrefix,
        string? description = null
    )
    {
        var project = new ProjectModel
        {
            TenantId = tenantId,
            Name = name,
            KeyPrefix = keyPrefix,
            Description = description,
        };

        _dbContext.Projects.Add(project);

        try
        {
            await _dbContext.SaveChangesAsync();

            return project;
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("Unable to create new project", ex);
        }
    }

    // READ
    public async Task<ProjectModel?> GetProjectByIdAsync(Guid projectId)
    {
        return await _dbContext
            .Projects.AsNoTracking()
            .Where(a => a.Id == projectId)
            .FirstOrDefaultAsync();
    }

    public async Task<ProjectModel?> GetProjectByKeyPrefixAsync(Guid tenantId, string keyPrefix)
    {
        return await _dbContext
            .Projects.AsNoTracking()
            .Where(a => a.KeyPrefix == keyPrefix)
            .Where(a => a.TenantId == tenantId)
            .FirstOrDefaultAsync();
    }

    public async Task<List<ProjectModel>> GetProjectsByTenantAsync(Guid tenantId)
    {
        return await _dbContext
            .Projects.AsNoTracking()
            .Where(a => a.TenantId == tenantId)
            .ToListAsync();
    }

    public async Task<List<ProjectModel>> GetActiveProjectsByTenantAsync(Guid tenantId)
    {
        return await _dbContext
            .Projects.AsNoTracking()
            .Where(a => a.TenantId == tenantId)
            .Where(a => a.IsArchived != true)
            .ToListAsync();
    }

    // UPDATE
    public async Task<ProjectModel> UpdateProjectAsync(
        Guid projectId,
        string? name = null,
        string? description = null
    )
    {
        var project =
            await _dbContext.Projects.Where(a => a.Id == projectId).FirstOrDefaultAsync()
            ?? throw new InvalidOperationException($"Project with ID {projectId} not found");

        if (name != null)
        {
            project.Name = name;
        }

        if (description != null)
        {
            project.Description = description;
        }

        _dbContext.Projects.Update(project);

        try
        {
            await _dbContext.SaveChangesAsync();

            return project;
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("Unable to update project", ex);
        }
    }

    // DELETE / ARCHIVE
    public async Task ArchiveProjectAsync(Guid projectId)
    {
        var project =
            await _dbContext.Projects.Where(a => a.Id == projectId).FirstOrDefaultAsync()
            ?? throw new InvalidOperationException($"Project with ID {projectId} not found");

        project.IsArchived = true;

        _dbContext.Projects.Update(project);

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("Unable to update project", ex);
        }
    }

    public async Task UnarchiveProjectAsync(Guid projectId)
    {
        var project =
            await _dbContext.Projects.Where(a => a.Id == projectId).FirstOrDefaultAsync()
            ?? throw new InvalidOperationException($"Project with ID {projectId} not found");

        project.IsArchived = false;

        _dbContext.Projects.Update(project);

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("Unable to update project", ex);
        }
    }

    public async Task DeleteProjectAsync(Guid projectId)
    {
        var project =
            await _dbContext.Projects.Where(a => a.Id == projectId).FirstOrDefaultAsync()
            ?? throw new InvalidOperationException($"Project with ID {projectId} not found");

        _dbContext.Projects.Remove(project);

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("Unable to update project", ex);
        }
    }

    // ADDITIONAL
    public async Task<int> GetProjectTaskCountAsync(Guid projectId)
    {
        return await _dbContext
            .Tasks.AsNoTracking()
            .Where(a => a.ProjectId == projectId)
            .CountAsync();
    }

    public async Task<List<TaskStatusModel>> GetProjectStatusesAsync(Guid projectId)
    {
        return await _dbContext
            .TaskStatuses.AsNoTracking()
            .Where(a => a.ProjectId == projectId)
            .ToListAsync();
    }
}

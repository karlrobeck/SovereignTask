using Microsoft.EntityFrameworkCore;

namespace TaskManagement;

public class TaskStatusService
{
    private readonly Context _dbContext;

    public TaskStatusService(Context dbContext)
    {
        _dbContext = dbContext;
    }

    // CREATE
    public async Task<TaskStatusModel> CreateStatusAsync(
        Guid projectId,
        string name,
        int position,
        bool isCompleted = false
    )
    {
        var status = new TaskStatusModel
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = name,
            Position = position,
            IsCompleted = isCompleted,
        };

        _dbContext.TaskStatuses.Add(status);

        try
        {
            await _dbContext.SaveChangesAsync();
            return status;
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("Failed to create task status", ex);
        }
    }

    // READ
    public async Task<TaskStatusModel?> GetStatusByIdAsync(Guid statusId)
    {
        return await _dbContext
            .TaskStatuses.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == statusId);
    }

    public async Task<List<TaskStatusModel>> GetStatusesByProjectAsync(Guid projectId)
    {
        return await _dbContext
            .TaskStatuses.AsNoTracking()
            .Where(s => s.ProjectId == projectId)
            .OrderBy(s => s.Position)
            .ToListAsync();
    }

    public async Task<TaskStatusModel?> GetCompletedStatusAsync(Guid projectId)
    {
        return await _dbContext
            .TaskStatuses.AsNoTracking()
            .FirstOrDefaultAsync(s => s.ProjectId == projectId && s.IsCompleted);
    }

    // UPDATE
    public async Task<TaskStatusModel> UpdateStatusAsync(
        Guid statusId,
        string? name = null,
        int? position = null,
        bool? isCompleted = null
    )
    {
        var status = await _dbContext.TaskStatuses.FirstOrDefaultAsync(s => s.Id == statusId);
        if (status == null)
            throw new InvalidOperationException($"Task status with ID {statusId} not found");

        if (!string.IsNullOrEmpty(name))
            status.Name = name;
        if (position.HasValue)
            status.Position = position.Value;
        if (isCompleted.HasValue)
            status.IsCompleted = isCompleted.Value;

        _dbContext.TaskStatuses.Update(status);

        try
        {
            await _dbContext.SaveChangesAsync();
            return status;
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("Failed to update task status", ex);
        }
    }

    // DELETE
    public async Task DeleteStatusAsync(Guid statusId)
    {
        var status = await _dbContext.TaskStatuses.FirstOrDefaultAsync(s => s.Id == statusId);
        if (status == null)
            throw new InvalidOperationException($"Task status with ID {statusId} not found");

        _dbContext.TaskStatuses.Remove(status);

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("Failed to delete task status", ex);
        }
    }

    // ADDITIONAL
    public async Task<int> GetTasksInStatusAsync(Guid statusId)
    {
        return await _dbContext.Tasks.Where(t => t.StatusId == statusId).CountAsync();
    }

    public async Task ReorderStatusesAsync(
        Guid projectId,
        List<(Guid statusId, int newPosition)> reordering
    )
    {
        var statuses = await _dbContext
            .TaskStatuses.Where(s => s.ProjectId == projectId)
            .ToListAsync();

        foreach (var (statusId, newPosition) in reordering)
        {
            var status = statuses.FirstOrDefault(s => s.Id == statusId);
            if (status != null)
                status.Position = newPosition;
        }

        _dbContext.TaskStatuses.UpdateRange(statuses);

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("Failed to reorder task statuses", ex);
        }
    }
}

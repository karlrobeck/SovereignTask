using Microsoft.EntityFrameworkCore;

namespace TaskManagement;

public class TaskService
{
    private readonly Context _dbContext;

    public TaskService(Context dbContext)
    {
        _dbContext = dbContext;
    }

    // CREATE
    public async Task<TaskModel> CreateTaskAsync(
        Guid projectId,
        string title,
        Guid statusId,
        string? description = null,
        Guid? parentId = null,
        Guid? assigneeId = null,
        int priority = 0,
        DateTime? startDate = null,
        DateTime? dueDate = null,
        int estimatedMinutes = 0
    )
    {
        var task = new TaskModel
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = title,
            StatusId = statusId,
            Description = description,
            ParentId = parentId,
            AssigneeId = assigneeId,
            Priority = priority,
            StartDate = startDate,
            DueDate = dueDate,
            EstimatedMinutes = estimatedMinutes,
            RowVersion = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _dbContext.Tasks.Add(task);

        try
        {
            await _dbContext.SaveChangesAsync();
            return task;
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("Failed to create task", ex);
        }
    }

    // READ
    public async Task<TaskModel?> GetTaskByIdAsync(Guid taskId)
    {
        return await _dbContext.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == taskId);
    }

    public async Task<List<TaskModel>> GetTasksByProjectAsync(Guid projectId)
    {
        return await _dbContext
            .Tasks.AsNoTracking()
            .Where(t => t.ProjectId == projectId)
            .ToListAsync();
    }

    public async Task<List<TaskModel>> GetTasksByStatusAsync(Guid statusId)
    {
        return await _dbContext
            .Tasks.AsNoTracking()
            .Where(t => t.StatusId == statusId)
            .ToListAsync();
    }

    public async Task<List<TaskModel>> GetTasksByAssigneeAsync(Guid? assigneeId)
    {
        return await _dbContext
            .Tasks.AsNoTracking()
            .Where(t => t.AssigneeId == assigneeId)
            .ToListAsync();
    }

    public async Task<List<TaskModel>> GetSubtasksAsync(Guid parentTaskId)
    {
        return await _dbContext
            .Tasks.AsNoTracking()
            .Where(t => t.ParentId == parentTaskId)
            .ToListAsync();
    }

    // UPDATE
    public async Task<TaskModel> UpdateTaskAsync(
        Guid taskId,
        string? title = null,
        string? description = null,
        Guid? statusId = null,
        Guid? assigneeId = null,
        int? priority = null,
        DateTime? startDate = null,
        DateTime? dueDate = null,
        int? estimatedMinutes = null
    )
    {
        var task = await _dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == taskId);
        if (task == null)
            throw new InvalidOperationException($"Task with ID {taskId} not found");

        if (!string.IsNullOrEmpty(title))
            task.Title = title;
        if (description != null)
            task.Description = description;
        if (statusId.HasValue)
            task.StatusId = statusId.Value;
        if (assigneeId.HasValue)
            task.AssigneeId = assigneeId.Value;
        if (priority.HasValue)
            task.Priority = priority.Value;
        if (startDate.HasValue)
            task.StartDate = startDate.Value;
        if (dueDate.HasValue)
            task.DueDate = dueDate.Value;
        if (estimatedMinutes.HasValue)
            task.EstimatedMinutes = estimatedMinutes.Value;

        task.UpdatedAt = DateTime.UtcNow;
        task.RowVersion++;

        _dbContext.Tasks.Update(task);

        try
        {
            await _dbContext.SaveChangesAsync();
            return task;
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("Failed to update task", ex);
        }
    }

    // DELETE
    public async Task DeleteTaskAsync(Guid taskId)
    {
        var task = await _dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == taskId);
        if (task == null)
            throw new InvalidOperationException($"Task with ID {taskId} not found");

        _dbContext.Tasks.Remove(task);

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("Failed to delete task", ex);
        }
    }

    // ADDITIONAL
    public async Task<List<TaskModel>> GetTasksByPriorityAsync(Guid projectId, int priority)
    {
        return await _dbContext
            .Tasks.AsNoTracking()
            .Where(t => t.ProjectId == projectId && t.Priority == priority)
            .ToListAsync();
    }

    public async Task<List<TaskModel>> GetOverdueTasksAsync(Guid projectId)
    {
        var now = DateTime.UtcNow;
        return await _dbContext
            .Tasks.AsNoTracking()
            .Where(t => t.ProjectId == projectId && t.DueDate < now)
            .ToListAsync();
    }

    public async Task<List<TaskModel>> GetUpcomingTasksAsync(Guid projectId, int daysAhead = 7)
    {
        var now = DateTime.UtcNow;
        var futureDate = now.AddDays(daysAhead);
        return await _dbContext
            .Tasks.AsNoTracking()
            .Where(t => t.ProjectId == projectId && t.StartDate >= now && t.StartDate <= futureDate)
            .ToListAsync();
    }

    public async Task<int> GetTotalEstimatedMinutesAsync(Guid taskId)
    {
        var task = await _dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == taskId);
        if (task == null)
            return 0;

        var subtasksTotal = await _dbContext
            .Tasks.Where(t => t.ParentId == taskId)
            .SumAsync(t => t.EstimatedMinutes);

        return task.EstimatedMinutes + subtasksTotal;
    }

    public async Task MoveTaskToStatusAsync(Guid taskId, Guid newStatusId)
    {
        var task = await _dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == taskId);
        if (task == null)
            throw new InvalidOperationException($"Task with ID {taskId} not found");

        task.StatusId = newStatusId;
        task.UpdatedAt = DateTime.UtcNow;
        task.RowVersion++;

        _dbContext.Tasks.Update(task);

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("Failed to move task to status", ex);
        }
    }

    public async Task AssignTaskAsync(Guid taskId, Guid userId)
    {
        var task = await _dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == taskId);
        if (task == null)
            throw new InvalidOperationException($"Task with ID {taskId} not found");

        task.AssigneeId = userId;
        task.UpdatedAt = DateTime.UtcNow;
        task.RowVersion++;

        _dbContext.Tasks.Update(task);

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("Failed to assign task", ex);
        }
    }

    public async Task UnassignTaskAsync(Guid taskId)
    {
        var task = await _dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == taskId);
        if (task == null)
            throw new InvalidOperationException($"Task with ID {taskId} not found");

        task.AssigneeId = null;
        task.UpdatedAt = DateTime.UtcNow;
        task.RowVersion++;

        _dbContext.Tasks.Update(task);

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("Failed to unassign task", ex);
        }
    }
}

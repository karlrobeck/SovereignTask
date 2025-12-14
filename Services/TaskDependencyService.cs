using Microsoft.EntityFrameworkCore;

namespace TaskManagement;

public class TaskDependencyService
{
    private readonly Context _dbContext;

    public TaskDependencyService(Context dbContext)
    {
        _dbContext = dbContext;
    }

    // CREATE
    public async Task<TaskDependencyModel> CreateDependencyAsync(
        Guid predecessorId,
        Guid successorId,
        string type = "FS"
    )
    {
        // Check for circular dependency
        var hasCircular = await HasCircularDependencyAsync(predecessorId, successorId);
        if (hasCircular)
            throw new InvalidOperationException(
                "Creating this dependency would create a circular reference"
            );

        var dependency = new TaskDependencyModel
        {
            Id = Guid.NewGuid(),
            PredecessorId = predecessorId,
            SuccessorId = successorId,
            Type = type,
        };

        _dbContext.TaskDependencies.Add(dependency);

        try
        {
            await _dbContext.SaveChangesAsync();
            return dependency;
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("Failed to create task dependency", ex);
        }
    }

    // READ
    public async Task<TaskDependencyModel?> GetDependencyByIdAsync(Guid dependencyId)
    {
        return await _dbContext
            .TaskDependencies.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == dependencyId);
    }

    public async Task<List<TaskDependencyModel>> GetPredecessorDependenciesAsync(Guid taskId)
    {
        return await _dbContext
            .TaskDependencies.AsNoTracking()
            .Where(d => d.SuccessorId == taskId)
            .ToListAsync();
    }

    public async Task<List<TaskDependencyModel>> GetSuccessorDependenciesAsync(Guid taskId)
    {
        return await _dbContext
            .TaskDependencies.AsNoTracking()
            .Where(d => d.PredecessorId == taskId)
            .ToListAsync();
    }

    public async Task<List<TaskModel>> GetBlockingTasksAsync(Guid taskId)
    {
        return await _dbContext
            .Tasks.AsNoTracking()
            .Where(t =>
                _dbContext.TaskDependencies.Any(d =>
                    d.SuccessorId == taskId && d.PredecessorId == t.Id
                )
            )
            .ToListAsync();
    }

    public async Task<List<TaskModel>> GetBlockedByTasksAsync(Guid taskId)
    {
        return await _dbContext
            .Tasks.AsNoTracking()
            .Where(t =>
                _dbContext.TaskDependencies.Any(d =>
                    d.PredecessorId == taskId && d.SuccessorId == t.Id
                )
            )
            .ToListAsync();
    }

    // UPDATE
    public async Task<TaskDependencyModel> UpdateDependencyAsync(Guid dependencyId, string type)
    {
        var dependency = await _dbContext.TaskDependencies.FirstOrDefaultAsync(d =>
            d.Id == dependencyId
        );
        if (dependency == null)
            throw new InvalidOperationException($"Dependency with ID {dependencyId} not found");

        dependency.Type = type;
        _dbContext.TaskDependencies.Update(dependency);

        try
        {
            await _dbContext.SaveChangesAsync();
            return dependency;
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("Failed to update task dependency", ex);
        }
    }

    // DELETE
    public async Task DeleteDependencyAsync(Guid dependencyId)
    {
        var dependency = await _dbContext.TaskDependencies.FirstOrDefaultAsync(d =>
            d.Id == dependencyId
        );
        if (dependency == null)
            throw new InvalidOperationException($"Dependency with ID {dependencyId} not found");

        _dbContext.TaskDependencies.Remove(dependency);

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("Failed to delete task dependency", ex);
        }
    }

    // ADDITIONAL
    public async Task<bool> HasCircularDependencyAsync(Guid predecessorId, Guid successorId)
    {
        var visited = new HashSet<Guid>();
        return await CheckCircularAsync(successorId, predecessorId, visited);
    }

    private async Task<bool> CheckCircularAsync(Guid current, Guid target, HashSet<Guid> visited)
    {
        if (current == target)
            return true;

        if (visited.Contains(current))
            return false;

        visited.Add(current);

        var successors = await _dbContext
            .TaskDependencies.Where(d => d.PredecessorId == current)
            .Select(d => d.SuccessorId)
            .ToListAsync();

        foreach (var successor in successors)
        {
            if (await CheckCircularAsync(successor, target, visited))
                return true;
        }

        return false;
    }

    public async Task<List<TaskModel>> GetCriticalPathAsync(Guid projectId)
    {
        // Get all tasks in the project with no dependencies (roots)
        var rootTasks = await _dbContext
            .Tasks.AsNoTracking()
            .Where(t =>
                t.ProjectId == projectId
                && !_dbContext.TaskDependencies.Any(d => d.SuccessorId == t.Id)
            )
            .ToListAsync();

        var criticalPath = new List<TaskModel>();

        foreach (var task in rootTasks)
        {
            await TraverseCriticalPathAsync(task.Id, criticalPath);
        }

        return criticalPath.OrderBy(t => t.StartDate).ToList();
    }

    private async Task TraverseCriticalPathAsync(Guid taskId, List<TaskModel> path)
    {
        var task = await _dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == taskId);
        if (task != null && !path.Contains(task))
            path.Add(task);

        var dependencies = await _dbContext
            .TaskDependencies.Where(d => d.PredecessorId == taskId)
            .Select(d => d.SuccessorId)
            .ToListAsync();

        foreach (var successor in dependencies)
        {
            await TraverseCriticalPathAsync(successor, path);
        }
    }
}

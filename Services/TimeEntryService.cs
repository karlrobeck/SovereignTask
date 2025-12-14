using Microsoft.EntityFrameworkCore;

namespace TaskManagement;

public class TimeEntryService
{
    private readonly Context _dbContext;

    public TimeEntryService(Context dbContext)
    {
        _dbContext = dbContext;
    }

    // CREATE
    public async Task<TimeEntryModel> CreateTimeEntryAsync(
        Guid taskId,
        Guid userId,
        DateTime startTime,
        string? description = null
    )
    {
        var timeEntry = new TimeEntryModel
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            UserId = userId,
            StartTime = startTime,
            EndTime = null,
            Description = description,
        };

        _dbContext.TimeEntries.Add(timeEntry);

        try
        {
            await _dbContext.SaveChangesAsync();
            return timeEntry;
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("Failed to create time entry", ex);
        }
    }

    // READ
    public async Task<TimeEntryModel?> GetTimeEntryByIdAsync(Guid entryId)
    {
        return await _dbContext
            .TimeEntries.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == entryId);
    }

    public async Task<List<TimeEntryModel>> GetTimeEntriesByTaskAsync(Guid taskId)
    {
        return await _dbContext
            .TimeEntries.AsNoTracking()
            .Where(t => t.TaskId == taskId)
            .ToListAsync();
    }

    public async Task<List<TimeEntryModel>> GetTimeEntriesByUserAsync(Guid userId)
    {
        return await _dbContext
            .TimeEntries.AsNoTracking()
            .Where(t => t.UserId == userId)
            .ToListAsync();
    }

    public async Task<TimeEntryModel?> GetActiveTimeEntryAsync(Guid userId)
    {
        return await _dbContext
            .TimeEntries.AsNoTracking()
            .FirstOrDefaultAsync(t => t.UserId == userId && t.EndTime == null);
    }

    public async Task<List<TimeEntryModel>> GetTimeEntriesByDateRangeAsync(
        Guid userId,
        DateTime startDate,
        DateTime endDate
    )
    {
        return await _dbContext
            .TimeEntries.AsNoTracking()
            .Where(t => t.UserId == userId && t.StartTime >= startDate && t.StartTime <= endDate)
            .ToListAsync();
    }

    // UPDATE
    public async Task<TimeEntryModel> UpdateTimeEntryAsync(
        Guid entryId,
        DateTime? startTime = null,
        DateTime? endTime = null,
        string? description = null
    )
    {
        var timeEntry = await _dbContext.TimeEntries.FirstOrDefaultAsync(t => t.Id == entryId);
        if (timeEntry == null)
            throw new InvalidOperationException($"Time entry with ID {entryId} not found");

        if (startTime.HasValue)
            timeEntry.StartTime = startTime.Value;
        if (endTime.HasValue)
            timeEntry.EndTime = endTime.Value;
        if (description != null)
            timeEntry.Description = description;

        _dbContext.TimeEntries.Update(timeEntry);

        try
        {
            await _dbContext.SaveChangesAsync();
            return timeEntry;
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("Failed to update time entry", ex);
        }
    }

    // DELETE
    public async Task DeleteTimeEntryAsync(Guid entryId)
    {
        var timeEntry = await _dbContext.TimeEntries.FirstOrDefaultAsync(t => t.Id == entryId);
        if (timeEntry == null)
            throw new InvalidOperationException($"Time entry with ID {entryId} not found");

        _dbContext.TimeEntries.Remove(timeEntry);

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("Failed to delete time entry", ex);
        }
    }

    // ADDITIONAL
    public async Task<TimeEntryModel> StopTimerAsync(Guid entryId)
    {
        var timeEntry = await _dbContext.TimeEntries.FirstOrDefaultAsync(t => t.Id == entryId);
        if (timeEntry == null)
            throw new InvalidOperationException($"Time entry with ID {entryId} not found");

        if (timeEntry.EndTime.HasValue)
            throw new InvalidOperationException("This timer has already been stopped");

        timeEntry.EndTime = DateTime.UtcNow;
        _dbContext.TimeEntries.Update(timeEntry);

        try
        {
            await _dbContext.SaveChangesAsync();
            return timeEntry;
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("Failed to stop timer", ex);
        }
    }

    public async Task<TimeSpan> GetTotalTimeTrackedAsync(Guid taskId)
    {
        var timeEntries = await _dbContext
            .TimeEntries.AsNoTracking()
            .Where(t => t.TaskId == taskId)
            .ToListAsync();

        var totalTime = TimeSpan.Zero;
        foreach (var entry in timeEntries)
        {
            if (entry.EndTime.HasValue)
            {
                totalTime += entry.EndTime.Value - entry.StartTime;
            }
            else
            {
                // Still running, calculate up to now
                totalTime += DateTime.UtcNow - entry.StartTime;
            }
        }

        return totalTime;
    }

    public async Task<TimeSpan> GetUserTotalTimeAsync(
        Guid userId,
        DateTime startDate,
        DateTime endDate
    )
    {
        var timeEntries = await _dbContext
            .TimeEntries.AsNoTracking()
            .Where(t => t.UserId == userId && t.StartTime >= startDate && t.StartTime <= endDate)
            .ToListAsync();

        var totalTime = TimeSpan.Zero;
        foreach (var entry in timeEntries)
        {
            if (entry.EndTime.HasValue)
            {
                totalTime += entry.EndTime.Value - entry.StartTime;
            }
        }

        return totalTime;
    }

    public async Task<Dictionary<Guid, TimeSpan>> GetTimePerTaskAsync(
        Guid userId,
        DateTime startDate,
        DateTime endDate
    )
    {
        var timeEntries = await _dbContext
            .TimeEntries.AsNoTracking()
            .Where(t => t.UserId == userId && t.StartTime >= startDate && t.StartTime <= endDate)
            .ToListAsync();

        var timePerTask = new Dictionary<Guid, TimeSpan>();

        foreach (var entry in timeEntries)
        {
            if (!entry.EndTime.HasValue)
                continue;

            var duration = entry.EndTime.Value - entry.StartTime;

            if (timePerTask.ContainsKey(entry.TaskId))
            {
                timePerTask[entry.TaskId] += duration;
            }
            else
            {
                timePerTask[entry.TaskId] = duration;
            }
        }

        return timePerTask;
    }
}

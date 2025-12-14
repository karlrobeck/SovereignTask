using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace TaskManagement;

/// <summary>
/// Service for managing audit logs - demonstrates EF Core service patterns.
///
/// Key Concepts:
/// 1. Dependency Injection: DbContext is injected, not created manually
/// 2. Async Operations: All DB calls are async for better scalability
/// 3. LINQ Queries: Examples of filtering, sorting, pagination
/// 4. Related Entities: How to include navigation properties
/// 5. Transactions: For operations that need multiple changes
/// </summary>
public class AuditLogService
{
    private readonly Context _dbContext;

    // Constructor - receives DbContext via dependency injection
    public AuditLogService(Context dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// PATTERN 1: Creating/Logging a new audit entry
    /// This demonstrates: Add(), SaveChangesAsync(), error handling
    /// </summary>
    public async Task<AuditLogModel> LogChangeAsync(
        Guid tenantId,
        string entityTable,
        Guid entityId,
        string action, // 'CREATE', 'UPDATE', 'DELETE'
        Guid changedById,
        object? changesJson = null
    )
    {
        // Create a new entity instance
        var auditLog = new AuditLogModel
        {
            TenantId = tenantId,
            EntityTable = entityTable,
            EntityId = entityId,
            Action = action,
            ChangedById = changedById,
            ChangesJson = changesJson != null ? JsonSerializer.Serialize(changesJson) : null,
            CreatedAt = DateTime.UtcNow,
        };

        // Add to the DbSet (in-memory only at this point)
        _dbContext.AuditLogs.Add(auditLog);

        try
        {
            // Persist to database
            await _dbContext.SaveChangesAsync();
            return auditLog;
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException(
                $"Failed to log audit entry for {entityTable} {entityId}",
                ex
            );
        }
    }

    /// <summary>
    /// PATTERN 2: Reading with filtering
    /// This demonstrates: Where() filtering, AsNoTracking(), ToListAsync()
    ///
    /// AsNoTracking() = Read-only queries (better performance when you don't need updates)
    /// </summary>
    public async Task<List<AuditLogModel>> GetAuditLogsByEntityAsync(
        Guid tenantId,
        string entityTable,
        Guid entityId
    )
    {
        return await _dbContext
            .AuditLogs.AsNoTracking() // Tell EF we won't modify these
            .Where(a =>
                a.TenantId == tenantId && a.EntityTable == entityTable && a.EntityId == entityId
            )
            .OrderByDescending(a => a.CreatedAt) // Newest first
            .ToListAsync();
    }

    /// <summary>
    /// PATTERN 3: Reading with related data (Include)
    /// This demonstrates: Include() for navigation properties
    ///
    /// Include() = Load related entities to avoid N+1 queries
    /// Without Include, accessing .ChangedBy would trigger a separate query per record
    /// </summary>
    public async Task<List<AuditLogModel>> GetAuditLogsWithUserAsync(
        Guid tenantId,
        DateTime startDate,
        DateTime endDate
    )
    {
        return await _dbContext
            .AuditLogs.AsNoTracking()
            .Include(a => a.ChangedBy) // Load the User who made the change
            .Include(a => a.Tenant)
            .Where(a =>
                a.TenantId == tenantId && a.CreatedAt >= startDate && a.CreatedAt <= endDate
            )
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// PATTERN 4: Pagination
    /// This demonstrates: Skip(), Take(), Count()
    ///
    /// Use for displaying data in pages on UI
    /// </summary>
    public async Task<(List<AuditLogModel> items, int totalCount)> GetAuditLogsPagedAsync(
        Guid tenantId,
        int pageNumber = 1,
        int pageSize = 20
    )
    {
        var query = _dbContext.AuditLogs.AsNoTracking().Where(a => a.TenantId == tenantId);

        // Get total count for pagination metadata
        int totalCount = await query.CountAsync();

        // Skip to the right page and take N records
        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((pageNumber - 1) * pageSize) // Skip records from previous pages
            .Take(pageSize) // Take only pageSize records
            .ToListAsync();

        return (items, totalCount);
    }

    /// <summary>
    /// PATTERN 5: Aggregation queries
    /// This demonstrates: GroupBy(), Count(), Where on IEnumerable
    ///
    /// Use for statistics and analytics
    /// </summary>
    public async Task<Dictionary<string, int>> GetActionCountsByUserAsync(Guid tenantId)
    {
        // Group audit logs by who made the changes and count actions
        var results = await _dbContext
            .AuditLogs.AsNoTracking()
            .Where(a => a.TenantId == tenantId)
            .GroupBy(a => a.ChangedById)
            .Select(g => new { UserId = g.Key, ActionCount = g.Count() })
            .ToListAsync();

        // Convert to dictionary for easier access
        return results.ToDictionary(r => r.UserId.ToString(), r => r.ActionCount);
    }

    /// <summary>
    /// PATTERN 6: Finding a single record
    /// This demonstrates: FirstOrDefaultAsync(), SingleOrDefaultAsync()
    ///
    /// FirstOrDefault = Returns first match or null (safe, flexible)
    /// SingleOrDefault = Returns exactly 1 match or null (throws if >1 match)
    /// </summary>
    public async Task<AuditLogModel?> GetMostRecentChangeAsync(
        Guid tenantId,
        string entityTable,
        Guid entityId
    )
    {
        return await _dbContext
            .AuditLogs.AsNoTracking()
            .Where(a =>
                a.TenantId == tenantId && a.EntityTable == entityTable && a.EntityId == entityId
            )
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(); // Could also use SingleOrDefaultAsync if expecting exactly 1
    }

    /// <summary>
    /// PATTERN 7: Deleting records
    /// This demonstrates: Remove(), SaveChangesAsync()
    ///
    /// Usually you don't delete audit logs (they're immutable), but here's how
    /// </summary>
    public async Task DeleteOldAuditLogsAsync(Guid tenantId, DateTime beforeDate)
    {
        // Find records to delete
        var oldLogs = await _dbContext
            .AuditLogs.Where(a => a.TenantId == tenantId && a.CreatedAt < beforeDate)
            .ToListAsync();

        if (oldLogs.Count == 0)
            return;

        // Remove from DbSet
        _dbContext.AuditLogs.RemoveRange(oldLogs);

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException($"Failed to delete {oldLogs.Count} audit logs", ex);
        }
    }

    /// <summary>
    /// PATTERN 8: Complex queries with multiple conditions
    /// This demonstrates: Chaining Where() calls, Any()
    /// </summary>
    public async Task<List<AuditLogModel>> SearchAuditLogsAsync(
        Guid tenantId,
        string? entityTable = null,
        string? action = null,
        DateTime? startDate = null,
        DateTime? endDate = null
    )
    {
        var query = _dbContext.AuditLogs.AsNoTracking().Where(a => a.TenantId == tenantId);

        // Chain Where() calls conditionally
        if (!string.IsNullOrEmpty(entityTable))
            query = query.Where(a => a.EntityTable == entityTable);

        if (!string.IsNullOrEmpty(action))
            query = query.Where(a => a.Action == action);

        if (startDate.HasValue)
            query = query.Where(a => a.CreatedAt >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(a => a.CreatedAt <= endDate.Value);

        return await query.OrderByDescending(a => a.CreatedAt).ToListAsync();
    }

    /// <summary>
    /// PATTERN 9: Transactions for multiple operations
    /// This demonstrates: transactions when you need all-or-nothing behavior
    /// </summary>
    public async Task LogMultipleChangesAsync(
        Guid tenantId,
        List<(string table, Guid id, string action, Guid userId, object? changes)> changes
    )
    {
        // Start a transaction - if any operation fails, all rollback
        using var transaction = await _dbContext.Database.BeginTransactionAsync();

        try
        {
            foreach (var (table, id, action, userId, _changes) in changes)
            {
                await LogChangeAsync(tenantId, table, id, action, userId, _changes);
            }

            // Commit all changes
            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            // Rollback automatically if exception occurs
            await transaction.RollbackAsync();
            throw new InvalidOperationException("Failed to log multiple changes", ex);
        }
    }
}

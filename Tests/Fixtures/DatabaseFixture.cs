using Microsoft.EntityFrameworkCore;
using TaskManagement;
using TaskManagement.Models;
using Xunit;

namespace TaskManagement.Tests;

/// <summary>
/// Base fixture for integration tests using SQLite database.
/// Provides a clean database context for each test using a temporary SQLite file.
/// </summary>
public class DatabaseFixture : IAsyncLifetime
{
    private readonly DbContextOptions<Context> _options;
    private readonly string _databasePath;
    public Context DbContext { get; private set; } = null!;

    public DatabaseFixture()
    {
        // Create a temporary SQLite database file
        _databasePath = Path.Combine(Path.GetTempPath(), $"taskmanagement_test_{Guid.NewGuid()}.db");
        var connectionString = $"Data Source={_databasePath};";

        // Configure SQLite database with temporary file
        _options = new DbContextOptionsBuilder<Context>()
            .UseSqlite(connectionString)
            .Options;
    }

    public async Task InitializeAsync()
    {
        DbContext = new Context(_options);
        await DbContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (DbContext != null)
        {
            await DbContext.Database.EnsureDeletedAsync();
            await DbContext.DisposeAsync();
        }

        // Clean up temporary database file
        try
        {
            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    /// <summary>
    /// Clear all data from the database (called between tests to reset state)
    /// </summary>
    public async Task ClearAsync()
    {
        // Dispose the current context and create a fresh one
        await DbContext.DisposeAsync();
        DbContext = new Context(_options);

        // Delete in reverse order of foreign key dependencies
        // Get all entities
        var auditLogs = DbContext.AuditLogs.ToList();
        var magicLinks = DbContext.MagicLinks.ToList();
        var timeEntries = DbContext.TimeEntries.ToList();
        var taskDependencies = DbContext.TaskDependencies.ToList();
        var tasks = DbContext.Tasks.ToList();
        var taskStatuses = DbContext.TaskStatuses.ToList();
        var projects = DbContext.Projects.ToList();
        var users = DbContext.Users.ToList();
        var tenants = DbContext.Tenants.ToList();

        // Remove all entities
        DbContext.AuditLogs.RemoveRange(auditLogs);
        DbContext.MagicLinks.RemoveRange(magicLinks);
        DbContext.TimeEntries.RemoveRange(timeEntries);
        DbContext.TaskDependencies.RemoveRange(taskDependencies);
        DbContext.Tasks.RemoveRange(tasks);
        DbContext.TaskStatuses.RemoveRange(taskStatuses);
        DbContext.Projects.RemoveRange(projects);
        DbContext.Users.RemoveRange(users);
        DbContext.Tenants.RemoveRange(tenants);

        // Save changes
        await DbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Get a fresh DbContext instance for testing
    /// </summary>
    public Context CreateDbContext()
    {
        return new Context(_options);
    }
}

using TaskManagement;
using TaskManagement.Models;
using TaskManagement.Tests;
using Xunit;

namespace TaskManagement.Tests.Services;

[Collection("Database collection")]
public class AuditLogServiceTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private AuditLogService _service = null!;

    public AuditLogServiceTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        // Clear database before each test for isolation
        await _fixture.ClearAsync();
        // Create a fresh service using the fixture's DbContext
        _service = new AuditLogService(_fixture.DbContext);
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await Task.CompletedTask;
    }

    [Fact]
    public async Task LogChangeAsync_WithValidData_CreatesAuditLog()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var user = TestDataBuilder.CreateUser(tenant.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Users.Add(user);
        await _fixture.DbContext.SaveChangesAsync();

        var entityTable = "tasks";
        var entityId = Guid.NewGuid();
        var action = "CREATE";

        // Act
        var result = await _service.LogChangeAsync(
            tenantId: tenant.Id,
            entityTable: entityTable,
            entityId: entityId,
            action: action,
            changedById: user.Id
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(tenant.Id, result.TenantId);
        Assert.Equal(entityTable, result.EntityTable);
        Assert.Equal(entityId, result.EntityId);
        Assert.Equal(action, result.Action);
        Assert.Equal(user.Id, result.ChangedById);
    }

    [Fact]
    public async Task LogChangeAsync_WithChangesJson_SerializesChanges()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var user = TestDataBuilder.CreateUser(tenant.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Users.Add(user);
        await _fixture.DbContext.SaveChangesAsync();

        var changes = new { OldValue = "Old Title", NewValue = "New Title" };

        // Act
        var result = await _service.LogChangeAsync(
            tenantId: tenant.Id,
            entityTable: "tasks",
            entityId: Guid.NewGuid(),
            action: "UPDATE",
            changedById: user.Id,
            changesJson: changes
        );

        // Assert
        Assert.NotNull(result.ChangesJson);
        Assert.Contains("OldValue", result.ChangesJson);
        Assert.Contains("NewValue", result.ChangesJson);
    }

    [Fact]
    public async Task GetAuditLogsByEntityAsync_ReturnsAllLogsForEntity()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var user = TestDataBuilder.CreateUser(tenant.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Users.Add(user);
        await _fixture.DbContext.SaveChangesAsync();

        var entityId = Guid.NewGuid();

        var logs = new[]
        {
            await _service.LogChangeAsync(tenant.Id, "tasks", entityId, "CREATE", user.Id),
            await _service.LogChangeAsync(tenant.Id, "tasks", entityId, "UPDATE", user.Id),
            await _service.LogChangeAsync(tenant.Id, "tasks", entityId, "UPDATE", user.Id),
        };

        // Act
        var result = await _service.GetAuditLogsByEntityAsync(tenant.Id, "tasks", entityId);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.All(result, log => Assert.Equal(entityId, log.EntityId));
        // Verify newest first
        Assert.Equal("UPDATE", result[0].Action);
    }

    [Fact]
    public async Task GetAuditLogsByEntityAsync_WithDifferentEntity_ReturnsEmpty()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var user = TestDataBuilder.CreateUser(tenant.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Users.Add(user);
        await _fixture.DbContext.SaveChangesAsync();

        var entityId = Guid.NewGuid();
        await _service.LogChangeAsync(tenant.Id, "tasks", entityId, "CREATE", user.Id);

        // Act
        var result = await _service.GetAuditLogsByEntityAsync(tenant.Id, "projects", entityId);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAuditLogsWithUserAsync_IncludesUserData()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var user = TestDataBuilder.CreateUser(tenant.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Users.Add(user);
        await _fixture.DbContext.SaveChangesAsync();

        await _service.LogChangeAsync(tenant.Id, "tasks", Guid.NewGuid(), "CREATE", user.Id);

        var startDate = DateTime.UtcNow.AddHours(-1);
        var endDate = DateTime.UtcNow.AddHours(1);

        // Act
        var result = await _service.GetAuditLogsWithUserAsync(tenant.Id, startDate, endDate);

        // Assert
        Assert.NotEmpty(result);
        Assert.NotNull(result[0].ChangedBy);
        Assert.Equal(user.Id, result[0].ChangedBy!.Id);
    }

    [Fact]
    public async Task GetAuditLogsPagedAsync_ReturnsPaginatedResults()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var user = TestDataBuilder.CreateUser(tenant.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Users.Add(user);
        await _fixture.DbContext.SaveChangesAsync();

        // Create 25 audit logs
        for (int i = 0; i < 25; i++)
        {
            await _service.LogChangeAsync(tenant.Id, "tasks", Guid.NewGuid(), "CREATE", user.Id);
        }

        // Act
        var (items, totalCount) = await _service.GetAuditLogsPagedAsync(
            tenant.Id,
            pageNumber: 1,
            pageSize: 10
        );

        // Assert
        Assert.Equal(10, items.Count);
        Assert.Equal(25, totalCount);
    }

    [Fact]
    public async Task GetAuditLogsPagedAsync_WithSecondPage_ReturnsCorrectRecords()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var user = TestDataBuilder.CreateUser(tenant.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Users.Add(user);
        await _fixture.DbContext.SaveChangesAsync();

        for (int i = 0; i < 25; i++)
        {
            await _service.LogChangeAsync(tenant.Id, "tasks", Guid.NewGuid(), "CREATE", user.Id);
        }

        // Act
        var (items, _) = await _service.GetAuditLogsPagedAsync(
            tenant.Id,
            pageNumber: 2,
            pageSize: 10
        );

        // Assert
        Assert.Equal(10, items.Count);
    }

    [Fact]
    public async Task GetActionCountsByUserAsync_CountsActionsByUser()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var user1 = TestDataBuilder.CreateUser(tenant.Id, email: "user1@example.com");
        var user2 = TestDataBuilder.CreateUser(tenant.Id, email: "user2@example.com");

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Users.AddRange(user1, user2);
        await _fixture.DbContext.SaveChangesAsync();

        // User1: 3 changes, User2: 2 changes
        for (int i = 0; i < 3; i++)
        {
            await _service.LogChangeAsync(tenant.Id, "tasks", Guid.NewGuid(), "CREATE", user1.Id);
        }

        for (int i = 0; i < 2; i++)
        {
            await _service.LogChangeAsync(
                tenant.Id,
                "projects",
                Guid.NewGuid(),
                "UPDATE",
                user2.Id
            );
        }

        // Act
        var result = await _service.GetActionCountsByUserAsync(tenant.Id);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(3, result[user1.Id.ToString()]);
        Assert.Equal(2, result[user2.Id.ToString()]);
    }

    [Fact]
    public async Task GetMostRecentChangeAsync_ReturnsLatestLog()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var user = TestDataBuilder.CreateUser(tenant.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Users.Add(user);
        await _fixture.DbContext.SaveChangesAsync();

        var entityId = Guid.NewGuid();

        await _service.LogChangeAsync(tenant.Id, "tasks", entityId, "CREATE", user.Id);
        await Task.Delay(10); // Small delay to ensure different timestamps
        var mostRecent = await _service.LogChangeAsync(
            tenant.Id,
            "tasks",
            entityId,
            "UPDATE",
            user.Id
        );

        // Act
        var result = await _service.GetMostRecentChangeAsync(tenant.Id, "tasks", entityId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("UPDATE", result.Action);
        Assert.Equal(mostRecent.Id, result.Id);
    }

    [Fact]
    public async Task GetMostRecentChangeAsync_WithNonExistentEntity_ReturnsNull()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        _fixture.DbContext.Tenants.Add(tenant);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetMostRecentChangeAsync(tenant.Id, "tasks", Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteOldAuditLogsAsync_RemovesOldLogs()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var user = TestDataBuilder.CreateUser(tenant.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Users.Add(user);
        await _fixture.DbContext.SaveChangesAsync();

        // Create logs with different dates
        var oldDate = DateTime.UtcNow.AddDays(-10);
        var recentDate = DateTime.UtcNow.AddHours(-1);

        var oldLog = new AuditLogModel
        {
            TenantId = tenant.Id,
            EntityTable = "tasks",
            EntityId = Guid.NewGuid(),
            Action = "CREATE",
            ChangedById = user.Id,
            CreatedAt = oldDate,
        };

        var recentLog = new AuditLogModel
        {
            TenantId = tenant.Id,
            EntityTable = "tasks",
            EntityId = Guid.NewGuid(),
            Action = "CREATE",
            ChangedById = user.Id,
            CreatedAt = recentDate,
        };

        _fixture.DbContext.AuditLogs.AddRange(oldLog, recentLog);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        await _service.DeleteOldAuditLogsAsync(tenant.Id, DateTime.UtcNow.AddDays(-5));

        // Assert
        var remaining = _fixture.DbContext.AuditLogs.Count();
        Assert.Equal(1, remaining);
    }

    [Fact]
    public async Task SearchAuditLogsAsync_FiltersByEntityTable()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var user = TestDataBuilder.CreateUser(tenant.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Users.Add(user);
        await _fixture.DbContext.SaveChangesAsync();

        await _service.LogChangeAsync(tenant.Id, "tasks", Guid.NewGuid(), "CREATE", user.Id);
        await _service.LogChangeAsync(tenant.Id, "projects", Guid.NewGuid(), "CREATE", user.Id);
        await _service.LogChangeAsync(tenant.Id, "tasks", Guid.NewGuid(), "UPDATE", user.Id);

        // Act
        var result = await _service.SearchAuditLogsAsync(tenant.Id, entityTable: "tasks");

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, log => Assert.Equal("tasks", log.EntityTable));
    }

    [Fact]
    public async Task SearchAuditLogsAsync_FiltersByAction()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var user = TestDataBuilder.CreateUser(tenant.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Users.Add(user);
        await _fixture.DbContext.SaveChangesAsync();

        await _service.LogChangeAsync(tenant.Id, "tasks", Guid.NewGuid(), "CREATE", user.Id);
        await _service.LogChangeAsync(tenant.Id, "tasks", Guid.NewGuid(), "CREATE", user.Id);
        await _service.LogChangeAsync(tenant.Id, "tasks", Guid.NewGuid(), "UPDATE", user.Id);

        // Act
        var result = await _service.SearchAuditLogsAsync(tenant.Id, action: "CREATE");

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, log => Assert.Equal("CREATE", log.Action));
    }

    [Fact]
    public async Task SearchAuditLogsAsync_FiltersByDateRange()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var user = TestDataBuilder.CreateUser(tenant.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Users.Add(user);
        await _fixture.DbContext.SaveChangesAsync();

        var startDate = DateTime.UtcNow;
        var endDate = DateTime.UtcNow.AddHours(1);

        await _service.LogChangeAsync(tenant.Id, "tasks", Guid.NewGuid(), "CREATE", user.Id);

        // Act
        var result = await _service.SearchAuditLogsAsync(
            tenant.Id,
            startDate: startDate,
            endDate: endDate
        );

        // Assert
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task LogMultipleChangesAsync_LogsAllChanges()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var user = TestDataBuilder.CreateUser(tenant.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Users.Add(user);
        await _fixture.DbContext.SaveChangesAsync();

        var changes = new[]
        {
            ("tasks", Guid.NewGuid(), "CREATE", user.Id, (object?)null),
            ("projects", Guid.NewGuid(), "UPDATE", user.Id, (object?)null),
            ("users", Guid.NewGuid(), "DELETE", user.Id, (object?)null),
        };

        // Act
        await _service.LogMultipleChangesAsync(tenant.Id, changes.ToList());

        // Assert
        var result = _fixture.DbContext.AuditLogs.Where(a => a.TenantId == tenant.Id).ToList();
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task LogMultipleChangesAsync_RollsBackOnError()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var user = TestDataBuilder.CreateUser(tenant.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Users.Add(user);
        await _fixture.DbContext.SaveChangesAsync();

        // Create changes with invalid user ID to trigger error
        var changes = new[]
        {
            ("tasks", Guid.NewGuid(), "CREATE", user.Id, (object?)null),
            ("projects", Guid.NewGuid(), "UPDATE", Guid.NewGuid(), (object?)null), // Non-existent user
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.LogMultipleChangesAsync(tenant.Id, changes.ToList())
        );

        // Verify rollback - only first log should exist or none depending on transaction
        var logs = _fixture.DbContext.AuditLogs.Where(a => a.TenantId == tenant.Id).ToList();
        Assert.Empty(logs); // All should be rolled back
    }
}


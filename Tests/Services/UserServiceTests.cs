using TaskManagement;
using TaskManagement.Models;
using TaskManagement.Tests;
using Xunit;

namespace TaskManagement.Tests.Services;

[Collection("Database collection")]
public class UserServiceTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private UserService _service = null!;

    public UserServiceTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        // Clear database before each test for isolation
        await _fixture.ClearAsync();
        // Create a fresh service using the fixture's DbContext
        _service = new UserService(_fixture.DbContext);
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await Task.CompletedTask;
    }

    [Fact]
    public async Task CreateUserAsync_WithValidData_CreatesUser()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        _fixture.DbContext.Tenants.Add(tenant);
        await _fixture.DbContext.SaveChangesAsync();

        var email = "john.doe@example.com";
        var displayName = "John Doe";
        var role = "admin";

        // Act
        var result = await _service.CreateUserAsync(
            tenantId: tenant.Id,
            email: email,
            displayName: displayName,
            role: role
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(email, result.Email);
        Assert.Equal(displayName, result.DisplayName);
        Assert.Equal(role, result.Role);
        Assert.Equal(tenant.Id, result.TenantId);
        Assert.Null(result.EntraOid);
    }

    [Fact]
    public async Task CreateUserAsync_WithEntraOid_CreatesUserWithSSOMapping()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        _fixture.DbContext.Tenants.Add(tenant);
        await _fixture.DbContext.SaveChangesAsync();

        var entraOid = "12345678-1234-1234-1234-123456789012";

        // Act
        var result = await _service.CreateUserAsync(
            tenantId: tenant.Id,
            email: "user@example.com",
            displayName: "Test User",
            role: "member",
            entraOid: entraOid
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(entraOid, result.EntraOid);
    }

    [Fact]
    public async Task GetUserByIdAsync_WithExistingUser_ReturnsUser()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var user = TestDataBuilder.CreateUser(tenant.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Users.Add(user);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetUserByIdAsync(user.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.Id, result.Id);
        Assert.Equal(user.Email, result.Email);
    }

    [Fact]
    public async Task GetUserByIdAsync_WithNonExistentId_ReturnsNull()
    {
        // Act
        var result = await _service.GetUserByIdAsync(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetUserByEmailAsync_WithExistingEmail_ReturnsUser()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var email = "unique@example.com";
        var user = TestDataBuilder.CreateUser(tenant.Id, email: email);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Users.Add(user);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetUserByEmailAsync(tenant.Id, email);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(email, result.Email);
    }

    [Fact]
    public async Task GetUserByEmailAsync_WithNonExistentEmail_ReturnsNull()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        _fixture.DbContext.Tenants.Add(tenant);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetUserByEmailAsync(tenant.Id, "nonexistent@example.com");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetUserByEntraOidAsync_WithExistingEntraOid_ReturnsUser()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var entraOid = "oid-12345";
        var user = TestDataBuilder.CreateUser(tenant.Id, entraOid: entraOid);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Users.Add(user);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetUserByEntraOidAsync(tenant.Id, entraOid);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(entraOid, result.EntraOid);
    }

    [Fact]
    public async Task GetUserByEntraOidAsync_WithNonExistentEntraOid_ReturnsNull()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        _fixture.DbContext.Tenants.Add(tenant);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetUserByEntraOidAsync(tenant.Id, "nonexistent-oid");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetUsersByTenantAsync_WithMultipleUsers_ReturnsAllTenantUsers()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var users = new[]
        {
            TestDataBuilder.CreateUser(tenant.Id, email: "user1@example.com"),
            TestDataBuilder.CreateUser(tenant.Id, email: "user2@example.com"),
            TestDataBuilder.CreateUser(tenant.Id, email: "user3@example.com"),
        };

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Users.AddRange(users);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetUsersByTenantAsync(tenant.Id);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.All(result, u => Assert.Equal(tenant.Id, u.TenantId));
    }

    [Fact]
    public async Task GetUsersByTenantAsync_WithNoUsers_ReturnsEmptyList()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        _fixture.DbContext.Tenants.Add(tenant);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetUsersByTenantAsync(tenant.Id);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetUsersByRoleAsync_WithMultipleUsers_ReturnsOnlyUsersWithRole()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var adminUsers = new[]
        {
            TestDataBuilder.CreateUser(tenant.Id, email: "admin1@example.com", role: "admin"),
            TestDataBuilder.CreateUser(tenant.Id, email: "admin2@example.com", role: "admin"),
        };
        var memberUser = TestDataBuilder.CreateUser(
            tenant.Id,
            email: "member@example.com",
            role: "member"
        );

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Users.AddRange(adminUsers);
        _fixture.DbContext.Users.Add(memberUser);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetUsersByRoleAsync(tenant.Id, "admin");

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, u => Assert.Equal("admin", u.Role));
    }

    [Fact]
    public async Task UpdateUserAsync_WithValidData_UpdatesUser()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var user = TestDataBuilder.CreateUser(tenant.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Users.Add(user);
        await _fixture.DbContext.SaveChangesAsync();

        var newEmail = "newemail@example.com";
        var newDisplayName = "Updated Name";
        var newRole = "admin";

        // Act
        var result = await _service.UpdateUserAsync(
            userId: user.Id,
            email: newEmail,
            displayName: newDisplayName,
            role: newRole
        );

        // Assert
        Assert.Equal(newEmail, result.Email);
        Assert.Equal(newDisplayName, result.DisplayName);
        Assert.Equal(newRole, result.Role);
    }

    [Fact]
    public async Task UpdateUserAsync_WithNonExistentUser_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UpdateUserAsync(Guid.NewGuid(), email: "test@example.com")
        );
    }

    [Fact]
    public async Task DeleteUserAsync_RemovesUser()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var user = TestDataBuilder.CreateUser(tenant.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Users.Add(user);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        await _service.DeleteUserAsync(user.Id);

        // Assert
        var result = await _service.GetUserByIdAsync(user.Id);
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteUserAsync_WithNonExistentUser_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.DeleteUserAsync(Guid.NewGuid())
        );
    }

    [Fact]
    public async Task GetUserTimeEntriesAsync_WithTimeEntries_ReturnsAll()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var user = TestDataBuilder.CreateUser(tenant.Id);
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var status = TestDataBuilder.CreateTaskStatus(project.Id);
        var task1 = TestDataBuilder.CreateTask(project.Id, status.Id);
        var task2 = TestDataBuilder.CreateTask(project.Id, status.Id);

        var entries = new[]
        {
            TestDataBuilder.CreateTimeEntry(task1.Id, user.Id),
            TestDataBuilder.CreateTimeEntry(task2.Id, user.Id),
        };

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Users.Add(user);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.TaskStatuses.Add(status);
        _fixture.DbContext.Tasks.AddRange(task1, task2);
        _fixture.DbContext.TimeEntries.AddRange(entries);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetUserTimeEntriesAsync(user.Id);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, e => Assert.Equal(user.Id, e.UserId));
    }

    [Fact]
    public async Task GetUserAssignedTasksAsync_WithAssignedTasks_ReturnsAll()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var user = TestDataBuilder.CreateUser(tenant.Id);
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var status = TestDataBuilder.CreateTaskStatus(project.Id);

        var tasks = new[]
        {
            TestDataBuilder.CreateTask(project.Id, status.Id, assigneeId: user.Id),
            TestDataBuilder.CreateTask(project.Id, status.Id, assigneeId: user.Id),
        };

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Users.Add(user);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.TaskStatuses.Add(status);
        _fixture.DbContext.Tasks.AddRange(tasks);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetUserAssignedTasksAsync(user.Id);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, t => Assert.Equal(user.Id, t.AssigneeId));
    }
}

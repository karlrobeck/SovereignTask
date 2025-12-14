using TaskManagement;
using TaskManagement.Models;
using TaskManagement.Tests;
using Xunit;

namespace TaskManagement.Tests.Services;

[Collection("Database collection")]
public class TaskStatusServiceTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private TaskStatusService _service = null!;

    public TaskStatusServiceTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        // Clear database before each test for isolation
        await _fixture.ClearAsync();
        // Create a fresh service using the fixture's DbContext
        _service = new TaskStatusService(_fixture.DbContext);
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await Task.CompletedTask;
    }

    [Fact]
    public async Task CreateStatusAsync_WithValidData_CreatesStatus()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        await _fixture.DbContext.SaveChangesAsync();

        var name = "In Progress";
        var position = 1;

        // Act
        var result = await _service.CreateStatusAsync(
            projectId: project.Id,
            name: name,
            position: position
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(name, result.Name);
        Assert.Equal(position, result.Position);
        Assert.Equal(project.Id, result.ProjectId);
        Assert.False(result.IsCompleted);
    }

    [Fact]
    public async Task CreateStatusAsync_WithCompletedFlag_CreatesCompletedStatus()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.CreateStatusAsync(
            projectId: project.Id,
            name: "Done",
            position: 2,
            isCompleted: true
        );

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public async Task GetStatusByIdAsync_WithExistingStatus_ReturnsStatus()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var status = TestDataBuilder.CreateTaskStatus(project.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.TaskStatuses.Add(status);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetStatusByIdAsync(status.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(status.Id, result.Id);
        Assert.Equal(status.Name, result.Name);
    }

    [Fact]
    public async Task GetStatusByIdAsync_WithNonExistentId_ReturnsNull()
    {
        // Act
        var result = await _service.GetStatusByIdAsync(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetStatusesByProjectAsync_ReturnsAllProjectStatuses()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);

        var statuses = new[]
        {
            TestDataBuilder.CreateTaskStatus(project.Id, name: "To Do", position: 0),
            TestDataBuilder.CreateTaskStatus(project.Id, name: "In Progress", position: 1),
            TestDataBuilder.CreateTaskStatus(project.Id, name: "In Review", position: 2),
            TestDataBuilder.CreateTaskStatus(
                project.Id,
                name: "Done",
                position: 3,
                isCompleted: true
            ),
        };

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.TaskStatuses.AddRange(statuses);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetStatusesByProjectAsync(project.Id);

        // Assert
        Assert.Equal(4, result.Count);
        Assert.All(result, s => Assert.Equal(project.Id, s.ProjectId));
        // Verify ordered by position
        Assert.Equal(0, result[0].Position);
        Assert.Equal(3, result[3].Position);
    }

    [Fact]
    public async Task GetStatusesByProjectAsync_WithNoStatuses_ReturnsEmptyList()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetStatusesByProjectAsync(project.Id);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetCompletedStatusAsync_ReturnsCompletedStatus()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);

        var statuses = new[]
        {
            TestDataBuilder.CreateTaskStatus(
                project.Id,
                name: "To Do",
                position: 0,
                isCompleted: false
            ),
            TestDataBuilder.CreateTaskStatus(
                project.Id,
                name: "Done",
                position: 1,
                isCompleted: true
            ),
        };

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.TaskStatuses.AddRange(statuses);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetCompletedStatusAsync(project.Id);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsCompleted);
        Assert.Equal("Done", result.Name);
    }

    [Fact]
    public async Task GetCompletedStatusAsync_WithNoCompletedStatus_ReturnsNull()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var status = TestDataBuilder.CreateTaskStatus(
            project.Id,
            name: "To Do",
            isCompleted: false
        );

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.TaskStatuses.Add(status);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetCompletedStatusAsync(project.Id);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateStatusAsync_WithValidData_UpdatesStatus()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var status = TestDataBuilder.CreateTaskStatus(project.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.TaskStatuses.Add(status);
        await _fixture.DbContext.SaveChangesAsync();

        var newName = "Under Review";
        var newPosition = 2;

        // Act
        var result = await _service.UpdateStatusAsync(
            statusId: status.Id,
            name: newName,
            position: newPosition
        );

        // Assert
        Assert.Equal(newName, result.Name);
        Assert.Equal(newPosition, result.Position);
    }

    [Fact]
    public async Task UpdateStatusAsync_WithCompletedFlag_UpdatesCompleted()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var status = TestDataBuilder.CreateTaskStatus(project.Id, isCompleted: false);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.TaskStatuses.Add(status);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.UpdateStatusAsync(statusId: status.Id, isCompleted: true);

        // Assert
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public async Task UpdateStatusAsync_WithNonExistentStatus_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UpdateStatusAsync(Guid.NewGuid(), name: "New Name")
        );
    }

    [Fact]
    public async Task DeleteStatusAsync_RemovesStatus()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var status = TestDataBuilder.CreateTaskStatus(project.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.TaskStatuses.Add(status);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        await _service.DeleteStatusAsync(status.Id);

        // Assert
        var result = await _service.GetStatusByIdAsync(status.Id);
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteStatusAsync_WithNonExistentStatus_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.DeleteStatusAsync(Guid.NewGuid())
        );
    }

    [Fact]
    public async Task GetTasksInStatusAsync_CountsTasksInStatus()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var status = TestDataBuilder.CreateTaskStatus(project.Id);

        var tasks = new[]
        {
            TestDataBuilder.CreateTask(project.Id, status.Id),
            TestDataBuilder.CreateTask(project.Id, status.Id),
            TestDataBuilder.CreateTask(project.Id, status.Id),
        };

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.TaskStatuses.Add(status);
        _fixture.DbContext.Tasks.AddRange(tasks);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetTasksInStatusAsync(status.Id);

        // Assert
        Assert.Equal(3, result);
    }

    [Fact]
    public async Task GetTasksInStatusAsync_WithNoTasks_ReturnsZero()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var status = TestDataBuilder.CreateTaskStatus(project.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.TaskStatuses.Add(status);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetTasksInStatusAsync(status.Id);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task ReorderStatusesAsync_UpdatesPositions()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);

        var statuses = new[]
        {
            TestDataBuilder.CreateTaskStatus(project.Id, name: "To Do", position: 0),
            TestDataBuilder.CreateTaskStatus(project.Id, name: "In Progress", position: 1),
            TestDataBuilder.CreateTaskStatus(project.Id, name: "Done", position: 2),
        };

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.TaskStatuses.AddRange(statuses);
        await _fixture.DbContext.SaveChangesAsync();

        // Get fresh instance to reorder
        var statusesToReorder = new[]
        {
            (statuses[0].Id, 2),
            (statuses[1].Id, 0),
            (statuses[2].Id, 1),
        };

        // Act
        await _service.ReorderStatusesAsync(project.Id, statusesToReorder.ToList());

        // Assert
        var result = await _service.GetStatusesByProjectAsync(project.Id);
        Assert.Equal(statuses[1].Id, result[0].Id); // In Progress is now at position 0
        Assert.Equal(statuses[2].Id, result[1].Id); // Done is now at position 1
        Assert.Equal(statuses[0].Id, result[2].Id); // To Do is now at position 2
    }
}

using TaskManagement;
using TaskManagement.Models;
using TaskManagement.Tests;
using Xunit;

namespace TaskManagement.Tests.Services;

[Collection("Database collection")]
public class TaskServiceTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private TaskService _service = null!;

    public TaskServiceTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        // Clear database before each test for isolation
        await _fixture.ClearAsync();
        // Create a fresh service using the fixture's DbContext
        _service = new TaskService(_fixture.DbContext);
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await Task.CompletedTask;
    }

    [Fact]
    public async Task CreateTaskAsync_WithValidData_CreatesTask()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var status = TestDataBuilder.CreateTaskStatus(project.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.TaskStatuses.Add(status);
        await _fixture.DbContext.SaveChangesAsync();

        var title = "Implement feature X";
        var priority = 2;

        // Act
        var result = await _service.CreateTaskAsync(
            projectId: project.Id,
            title: title,
            statusId: status.Id,
            priority: priority
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(title, result.Title);
        Assert.Equal(priority, result.Priority);
        Assert.Equal(status.Id, result.StatusId);
        Assert.Equal(1, result.RowVersion);
    }

    [Fact]
    public async Task GetTaskByIdAsync_WithExistingTask_ReturnsTask()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var status = TestDataBuilder.CreateTaskStatus(project.Id);
        var task = TestDataBuilder.CreateTask(project.Id, status.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.TaskStatuses.Add(status);
        _fixture.DbContext.Tasks.Add(task);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetTaskByIdAsync(task.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(task.Id, result.Id);
        Assert.Equal(task.Title, result.Title);
    }

    [Fact]
    public async Task GetTasksByProjectAsync_WithMultipleTasks_ReturnsAllProjectTasks()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var status = TestDataBuilder.CreateTaskStatus(project.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.TaskStatuses.Add(status);

        var tasks = new[]
        {
            TestDataBuilder.CreateTask(project.Id, status.Id, "Task 1"),
            TestDataBuilder.CreateTask(project.Id, status.Id, "Task 2"),
            TestDataBuilder.CreateTask(project.Id, status.Id, "Task 3"),
        };
        _fixture.DbContext.Tasks.AddRange(tasks);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetTasksByProjectAsync(project.Id);

        // Assert
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task UpdateTaskAsync_UpdatesTaskAndIncrementsRowVersion()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var status = TestDataBuilder.CreateTaskStatus(project.Id);
        var task = TestDataBuilder.CreateTask(project.Id, status.Id, "Original Title");

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.TaskStatuses.Add(status);
        _fixture.DbContext.Tasks.Add(task);
        await _fixture.DbContext.SaveChangesAsync();

        var newTitle = "Updated Title";
        var originalVersion = task.RowVersion;

        // Act
        var result = await _service.UpdateTaskAsync(task.Id, title: newTitle);

        // Assert
        Assert.Equal(newTitle, result.Title);
        Assert.Equal(originalVersion + 1, result.RowVersion);
    }

    [Fact]
    public async Task GetOverdueTasksAsync_WithPastDueDate_ReturnsOverdueTasks()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var status = TestDataBuilder.CreateTaskStatus(project.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.TaskStatuses.Add(status);

        var overdueTask = new TaskModel
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            StatusId = status.Id,
            Title = "Overdue Task",
            DueDate = DateTime.UtcNow.AddDays(-1), // Past due
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RowVersion = 1,
        };

        var futureTask = TestDataBuilder.CreateTask(project.Id, status.Id);
        futureTask.DueDate = DateTime.UtcNow.AddDays(10);

        _fixture.DbContext.Tasks.Add(overdueTask);
        _fixture.DbContext.Tasks.Add(futureTask);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetOverdueTasksAsync(project.Id);

        // Assert
        Assert.Single(result);
        Assert.Equal(overdueTask.Id, result[0].Id);
    }

    [Fact]
    public async Task AssignTaskAsync_AssignsTaskToUser()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var user = TestDataBuilder.CreateUser(tenant.Id);
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var status = TestDataBuilder.CreateTaskStatus(project.Id);
        var task = TestDataBuilder.CreateTask(project.Id, status.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Users.Add(user);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.TaskStatuses.Add(status);
        _fixture.DbContext.Tasks.Add(task);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        await _service.AssignTaskAsync(task.Id, user.Id);

        // Assert
        var result = await _service.GetTaskByIdAsync(task.Id);
        Assert.Equal(user.Id, result?.AssigneeId);
    }

    [Fact]
    public async Task UnassignTaskAsync_RemovesAssignee()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var user = TestDataBuilder.CreateUser(tenant.Id);
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var status = TestDataBuilder.CreateTaskStatus(project.Id);
        var task = TestDataBuilder.CreateTask(project.Id, status.Id, assigneeId: user.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Users.Add(user);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.TaskStatuses.Add(status);
        _fixture.DbContext.Tasks.Add(task);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        await _service.UnassignTaskAsync(task.Id);

        // Assert
        var result = await _service.GetTaskByIdAsync(task.Id);
        Assert.Null(result?.AssigneeId);
    }

    [Fact]
    public async Task GetSubtasksAsync_WithParentTask_ReturnsSubtasks()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var status = TestDataBuilder.CreateTaskStatus(project.Id);
        var parentTask = TestDataBuilder.CreateTask(project.Id, status.Id, "Parent Task");

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.TaskStatuses.Add(status);
        _fixture.DbContext.Tasks.Add(parentTask);
        await _fixture.DbContext.SaveChangesAsync();

        var subtasks = new[]
        {
            TestDataBuilder.CreateTask(project.Id, status.Id, "Subtask 1", parentId: parentTask.Id),
            TestDataBuilder.CreateTask(project.Id, status.Id, "Subtask 2", parentId: parentTask.Id),
        };
        _fixture.DbContext.Tasks.AddRange(subtasks);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetSubtasksAsync(parentTask.Id);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, t => Assert.Equal(parentTask.Id, t.ParentId));
    }

    [Fact]
    public async Task MoveTaskToStatusAsync_ChangesTaskStatus()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var status1 = TestDataBuilder.CreateTaskStatus(project.Id, "To Do", 0);
        var status2 = TestDataBuilder.CreateTaskStatus(project.Id, "In Progress", 1);
        var task = TestDataBuilder.CreateTask(project.Id, status1.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.TaskStatuses.AddRange(status1, status2);
        _fixture.DbContext.Tasks.Add(task);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        await _service.MoveTaskToStatusAsync(task.Id, status2.Id);

        // Assert
        var result = await _service.GetTaskByIdAsync(task.Id);
        Assert.Equal(status2.Id, result?.StatusId);
    }
}

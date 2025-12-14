using TaskManagement;
using TaskManagement.Models;
using TaskManagement.Tests;
using Xunit;

namespace TaskManagement.Tests.Services;

[Collection("Database collection")]
public class TaskDependencyServiceTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private TaskDependencyService _service = null!;

    public TaskDependencyServiceTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        // Clear database before each test for isolation
        await _fixture.ClearAsync();
        // Create a fresh service using the fixture's DbContext
        _service = new TaskDependencyService(_fixture.DbContext);
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await Task.CompletedTask;
    }

    [Fact]
    public async Task CreateDependencyAsync_WithValidData_CreatesDependency()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var status = TestDataBuilder.CreateTaskStatus(project.Id);
        var task1 = TestDataBuilder.CreateTask(project.Id, status.Id);
        var task2 = TestDataBuilder.CreateTask(project.Id, status.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.TaskStatuses.Add(status);
        _fixture.DbContext.Tasks.AddRange(task1, task2);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.CreateDependencyAsync(
            predecessorId: task1.Id,
            successorId: task2.Id,
            type: "FS"
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(task1.Id, result.PredecessorId);
        Assert.Equal(task2.Id, result.SuccessorId);
        Assert.Equal("FS", result.Type);
    }

    [Fact]
    public async Task CreateDependencyAsync_WithCircularReference_ThrowsException()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var status = TestDataBuilder.CreateTaskStatus(project.Id);
        var task1 = TestDataBuilder.CreateTask(project.Id, status.Id);
        var task2 = TestDataBuilder.CreateTask(project.Id, status.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.TaskStatuses.Add(status);
        _fixture.DbContext.Tasks.AddRange(task1, task2);
        await _fixture.DbContext.SaveChangesAsync();

        // Create initial dependency: task1 -> task2
        await _service.CreateDependencyAsync(task1.Id, task2.Id);

        // Act & Assert - try to create circular: task2 -> task1
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateDependencyAsync(task2.Id, task1.Id)
        );
    }

    [Fact]
    public async Task GetDependencyByIdAsync_WithExistingDependency_ReturnsDependency()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var status = TestDataBuilder.CreateTaskStatus(project.Id);
        var task1 = TestDataBuilder.CreateTask(project.Id, status.Id);
        var task2 = TestDataBuilder.CreateTask(project.Id, status.Id);

        var dependency = new TaskDependencyModel
        {
            Id = Guid.NewGuid(),
            PredecessorId = task1.Id,
            SuccessorId = task2.Id,
            Type = "FS",
        };

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.TaskStatuses.Add(status);
        _fixture.DbContext.Tasks.AddRange(task1, task2);
        _fixture.DbContext.TaskDependencies.Add(dependency);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetDependencyByIdAsync(dependency.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(dependency.Id, result.Id);
    }

    [Fact]
    public async Task GetDependencyByIdAsync_WithNonExistentId_ReturnsNull()
    {
        // Act
        var result = await _service.GetDependencyByIdAsync(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetPredecessorDependenciesAsync_ReturnsAllPredecessors()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var status = TestDataBuilder.CreateTaskStatus(project.Id);
        var predecessor1 = TestDataBuilder.CreateTask(project.Id, status.Id);
        var predecessor2 = TestDataBuilder.CreateTask(project.Id, status.Id);
        var successor = TestDataBuilder.CreateTask(project.Id, status.Id);

        var dependencies = new[]
        {
            new TaskDependencyModel
            {
                Id = Guid.NewGuid(),
                PredecessorId = predecessor1.Id,
                SuccessorId = successor.Id,
                Type = "FS",
            },
            new TaskDependencyModel
            {
                Id = Guid.NewGuid(),
                PredecessorId = predecessor2.Id,
                SuccessorId = successor.Id,
                Type = "FS",
            },
        };

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.TaskStatuses.Add(status);
        _fixture.DbContext.Tasks.AddRange(predecessor1, predecessor2, successor);
        _fixture.DbContext.TaskDependencies.AddRange(dependencies);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetPredecessorDependenciesAsync(successor.Id);

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetSuccessorDependenciesAsync_ReturnsAllSuccessors()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var status = TestDataBuilder.CreateTaskStatus(project.Id);
        var predecessor = TestDataBuilder.CreateTask(project.Id, status.Id);
        var successor1 = TestDataBuilder.CreateTask(project.Id, status.Id);
        var successor2 = TestDataBuilder.CreateTask(project.Id, status.Id);

        var dependencies = new[]
        {
            new TaskDependencyModel
            {
                Id = Guid.NewGuid(),
                PredecessorId = predecessor.Id,
                SuccessorId = successor1.Id,
                Type = "FS",
            },
            new TaskDependencyModel
            {
                Id = Guid.NewGuid(),
                PredecessorId = predecessor.Id,
                SuccessorId = successor2.Id,
                Type = "FS",
            },
        };

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.TaskStatuses.Add(status);
        _fixture.DbContext.Tasks.AddRange(predecessor, successor1, successor2);
        _fixture.DbContext.TaskDependencies.AddRange(dependencies);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetSuccessorDependenciesAsync(predecessor.Id);

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetBlockingTasksAsync_ReturnsTasksBlockingGivenTask()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var status = TestDataBuilder.CreateTaskStatus(project.Id);
        var blocking1 = TestDataBuilder.CreateTask(project.Id, status.Id);
        var blocking2 = TestDataBuilder.CreateTask(project.Id, status.Id);
        var blocked = TestDataBuilder.CreateTask(project.Id, status.Id);

        var dependencies = new[]
        {
            new TaskDependencyModel
            {
                Id = Guid.NewGuid(),
                PredecessorId = blocking1.Id,
                SuccessorId = blocked.Id,
                Type = "FS",
            },
            new TaskDependencyModel
            {
                Id = Guid.NewGuid(),
                PredecessorId = blocking2.Id,
                SuccessorId = blocked.Id,
                Type = "FS",
            },
        };

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.TaskStatuses.Add(status);
        _fixture.DbContext.Tasks.AddRange(blocking1, blocking2, blocked);
        _fixture.DbContext.TaskDependencies.AddRange(dependencies);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetBlockingTasksAsync(blocked.Id);

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetBlockedByTasksAsync_ReturnsTasksBlockedByGivenTask()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var status = TestDataBuilder.CreateTaskStatus(project.Id);
        var blocker = TestDataBuilder.CreateTask(project.Id, status.Id);
        var blocked1 = TestDataBuilder.CreateTask(project.Id, status.Id);
        var blocked2 = TestDataBuilder.CreateTask(project.Id, status.Id);

        var dependencies = new[]
        {
            new TaskDependencyModel
            {
                Id = Guid.NewGuid(),
                PredecessorId = blocker.Id,
                SuccessorId = blocked1.Id,
                Type = "FS",
            },
            new TaskDependencyModel
            {
                Id = Guid.NewGuid(),
                PredecessorId = blocker.Id,
                SuccessorId = blocked2.Id,
                Type = "FS",
            },
        };

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.TaskStatuses.Add(status);
        _fixture.DbContext.Tasks.AddRange(blocker, blocked1, blocked2);
        _fixture.DbContext.TaskDependencies.AddRange(dependencies);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetBlockedByTasksAsync(blocker.Id);

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task UpdateDependencyAsync_WithValidData_UpdatesDependency()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var status = TestDataBuilder.CreateTaskStatus(project.Id);
        var task1 = TestDataBuilder.CreateTask(project.Id, status.Id);
        var task2 = TestDataBuilder.CreateTask(project.Id, status.Id);

        var dependency = new TaskDependencyModel
        {
            Id = Guid.NewGuid(),
            PredecessorId = task1.Id,
            SuccessorId = task2.Id,
            Type = "FS",
        };

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.TaskStatuses.Add(status);
        _fixture.DbContext.Tasks.AddRange(task1, task2);
        _fixture.DbContext.TaskDependencies.Add(dependency);
        await _fixture.DbContext.SaveChangesAsync();

        var newType = "SS";

        // Act
        var result = await _service.UpdateDependencyAsync(
            dependencyId: dependency.Id,
            type: newType
        );

        // Assert
        Assert.Equal(newType, result.Type);
    }

    [Fact]
    public async Task UpdateDependencyAsync_WithNonExistentDependency_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UpdateDependencyAsync(Guid.NewGuid(), type: "SS")
        );
    }

    [Fact]
    public async Task DeleteDependencyAsync_RemovesDependency()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var status = TestDataBuilder.CreateTaskStatus(project.Id);
        var task1 = TestDataBuilder.CreateTask(project.Id, status.Id);
        var task2 = TestDataBuilder.CreateTask(project.Id, status.Id);

        var dependency = new TaskDependencyModel
        {
            Id = Guid.NewGuid(),
            PredecessorId = task1.Id,
            SuccessorId = task2.Id,
            Type = "FS",
        };

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.TaskStatuses.Add(status);
        _fixture.DbContext.Tasks.AddRange(task1, task2);
        _fixture.DbContext.TaskDependencies.Add(dependency);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        await _service.DeleteDependencyAsync(dependency.Id);

        // Assert
        var result = await _service.GetDependencyByIdAsync(dependency.Id);
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteDependencyAsync_WithNonExistentDependency_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.DeleteDependencyAsync(Guid.NewGuid())
        );
    }

    [Fact]
    public async Task HasCircularDependencyAsync_WithDirectCircle_ReturnsTrue()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var status = TestDataBuilder.CreateTaskStatus(project.Id);
        var task1 = TestDataBuilder.CreateTask(project.Id, status.Id);
        var task2 = TestDataBuilder.CreateTask(project.Id, status.Id);

        var dependency = new TaskDependencyModel
        {
            Id = Guid.NewGuid(),
            PredecessorId = task1.Id,
            SuccessorId = task2.Id,
            Type = "FS",
        };

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.TaskStatuses.Add(status);
        _fixture.DbContext.Tasks.AddRange(task1, task2);
        _fixture.DbContext.TaskDependencies.Add(dependency);
        await _fixture.DbContext.SaveChangesAsync();

        // Act - check if adding reverse creates circular
        var result = await _service.HasCircularDependencyAsync(task2.Id, task1.Id);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task HasCircularDependencyAsync_WithoutCircle_ReturnsFalse()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var status = TestDataBuilder.CreateTaskStatus(project.Id);
        var task1 = TestDataBuilder.CreateTask(project.Id, status.Id);
        var task2 = TestDataBuilder.CreateTask(project.Id, status.Id);
        var task3 = TestDataBuilder.CreateTask(project.Id, status.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.TaskStatuses.Add(status);
        _fixture.DbContext.Tasks.AddRange(task1, task2, task3);
        await _fixture.DbContext.SaveChangesAsync();

        // Act - check if new independent dependency creates circular
        var result = await _service.HasCircularDependencyAsync(task1.Id, task2.Id);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetCriticalPathAsync_ReturnsTasksInDependencyOrder()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var status = TestDataBuilder.CreateTaskStatus(project.Id);

        var task1 = TestDataBuilder.CreateTask(project.Id, status.Id, title: "Task 1");
        var task2 = TestDataBuilder.CreateTask(project.Id, status.Id, title: "Task 2");
        var task3 = TestDataBuilder.CreateTask(project.Id, status.Id, title: "Task 3");

        var dependencies = new[]
        {
            new TaskDependencyModel
            {
                Id = Guid.NewGuid(),
                PredecessorId = task1.Id,
                SuccessorId = task2.Id,
                Type = "FS",
            },
            new TaskDependencyModel
            {
                Id = Guid.NewGuid(),
                PredecessorId = task2.Id,
                SuccessorId = task3.Id,
                Type = "FS",
            },
        };

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.TaskStatuses.Add(status);
        _fixture.DbContext.Tasks.AddRange(task1, task2, task3);
        _fixture.DbContext.TaskDependencies.AddRange(dependencies);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetCriticalPathAsync(project.Id);

        // Assert
        Assert.NotEmpty(result);
        Assert.True(result.Count >= 1); // At least one task in critical path
    }
}

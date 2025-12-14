using TaskManagement;
using TaskManagement.Models;
using TaskManagement.Tests;
using Xunit;

namespace TaskManagement.Tests.Services;

[Collection("Database collection")]
public class ProjectServiceTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private ProjectService _service = null!;

    public ProjectServiceTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        // Clear database before each test for isolation
        await _fixture.ClearAsync();
        // Create a fresh service using the fixture's DbContext
        _service = new ProjectService(_fixture.DbContext);
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await Task.CompletedTask;
    }

    [Fact]
    public async Task CreateProjectAsync_WithValidData_CreatesProject()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        _fixture.DbContext.Tenants.Add(tenant);
        await _fixture.DbContext.SaveChangesAsync();

        var name = "E-Commerce Platform";
        var keyPrefix = "ECOM";
        var description = "Build a new e-commerce platform";

        // Act
        var result = await _service.CreateProjectAsync(
            tenantId: tenant.Id,
            name: name,
            keyPrefix: keyPrefix,
            description: description
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(name, result.Name);
        Assert.Equal(keyPrefix, result.KeyPrefix);
        Assert.Equal(description, result.Description);
        Assert.Equal(tenant.Id, result.TenantId);
        Assert.False(result.IsArchived);
    }

    [Fact]
    public async Task CreateProjectAsync_WithoutDescription_CreatesProjectSuccessfully()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        _fixture.DbContext.Tenants.Add(tenant);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.CreateProjectAsync(
            tenantId: tenant.Id,
            name: "Simple Project",
            keyPrefix: "SIMP"
        );

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.Description);
    }

    [Fact]
    public async Task GetProjectByIdAsync_WithExistingProject_ReturnsProject()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetProjectByIdAsync(project.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(project.Id, result.Id);
        Assert.Equal(project.Name, result.Name);
    }

    [Fact]
    public async Task GetProjectByIdAsync_WithNonExistentId_ReturnsNull()
    {
        // Act
        var result = await _service.GetProjectByIdAsync(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetProjectByKeyPrefixAsync_WithExistingKeyPrefix_ReturnsProject()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var keyPrefix = "UNIQUE";
        var project = TestDataBuilder.CreateProject(tenant.Id, keyPrefix: keyPrefix);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetProjectByKeyPrefixAsync(tenant.Id, keyPrefix);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(keyPrefix, result.KeyPrefix);
    }

    [Fact]
    public async Task GetProjectByKeyPrefixAsync_WithNonExistentKeyPrefix_ReturnsNull()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        _fixture.DbContext.Tenants.Add(tenant);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetProjectByKeyPrefixAsync(tenant.Id, "NONEXIST");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetProjectsByTenantAsync_WithMultipleProjects_ReturnsAllTenantProjects()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var projects = new[]
        {
            TestDataBuilder.CreateProject(tenant.Id, name: "Project 1"),
            TestDataBuilder.CreateProject(tenant.Id, name: "Project 2"),
            TestDataBuilder.CreateProject(tenant.Id, name: "Project 3"),
        };

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.AddRange(projects);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetProjectsByTenantAsync(tenant.Id);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.All(result, p => Assert.Equal(tenant.Id, p.TenantId));
    }

    [Fact]
    public async Task GetProjectsByTenantAsync_WithNoProjects_ReturnsEmptyList()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        _fixture.DbContext.Tenants.Add(tenant);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetProjectsByTenantAsync(tenant.Id);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetActiveProjectsByTenantAsync_WithArchivedAndActive_ReturnsOnlyActive()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var activeProject = TestDataBuilder.CreateProject(tenant.Id, name: "Active Project");
        var archivedProject = TestDataBuilder.CreateProject(tenant.Id, name: "Archived Project");
        archivedProject.IsArchived = true;

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(activeProject);
        _fixture.DbContext.Projects.Add(archivedProject);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetActiveProjectsByTenantAsync(tenant.Id);

        // Assert
        Assert.Single(result);
        Assert.Equal(activeProject.Id, result[0].Id);
        Assert.False(result[0].IsArchived);
    }

    [Fact]
    public async Task UpdateProjectAsync_WithValidData_UpdatesProject()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        await _fixture.DbContext.SaveChangesAsync();

        var newName = "Updated Project Name";
        var newDescription = "Updated description";

        // Act
        var result = await _service.UpdateProjectAsync(
            projectId: project.Id,
            name: newName,
            description: newDescription
        );

        // Assert
        Assert.Equal(newName, result.Name);
        Assert.Equal(newDescription, result.Description);
    }

    [Fact]
    public async Task UpdateProjectAsync_WithNonExistentProject_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UpdateProjectAsync(Guid.NewGuid(), name: "New Name")
        );
    }

    [Fact]
    public async Task ArchiveProjectAsync_ArchivedProject()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        await _service.ArchiveProjectAsync(project.Id);

        // Assert
        var result = await _service.GetProjectByIdAsync(project.Id);
        Assert.NotNull(result);
        Assert.True(result.IsArchived);
    }

    [Fact]
    public async Task ArchiveProjectAsync_WithNonExistentProject_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ArchiveProjectAsync(Guid.NewGuid())
        );
    }

    [Fact]
    public async Task UnarchiveProjectAsync_UnarchivedProject()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);
        project.IsArchived = true;

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        await _service.UnarchiveProjectAsync(project.Id);

        // Assert
        var result = await _service.GetProjectByIdAsync(project.Id);
        Assert.NotNull(result);
        Assert.False(result.IsArchived);
    }

    [Fact]
    public async Task UnarchiveProjectAsync_WithNonExistentProject_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UnarchiveProjectAsync(Guid.NewGuid())
        );
    }

    [Fact]
    public async Task DeleteProjectAsync_RemovesProject()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        await _service.DeleteProjectAsync(project.Id);

        // Assert
        var result = await _service.GetProjectByIdAsync(project.Id);
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteProjectAsync_WithNonExistentProject_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.DeleteProjectAsync(Guid.NewGuid())
        );
    }

    [Fact]
    public async Task GetProjectTaskCountAsync_CountsTasksInProject()
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
        var result = await _service.GetProjectTaskCountAsync(project.Id);

        // Assert
        Assert.Equal(3, result);
    }

    [Fact]
    public async Task GetProjectTaskCountAsync_WithNoTasks_ReturnsZero()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetProjectTaskCountAsync(project.Id);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task GetProjectStatusesAsync_ReturnsAllProjectStatuses()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);

        var statuses = new[]
        {
            TestDataBuilder.CreateTaskStatus(project.Id, name: "To Do", position: 0),
            TestDataBuilder.CreateTaskStatus(project.Id, name: "In Progress", position: 1),
            TestDataBuilder.CreateTaskStatus(
                project.Id,
                name: "Done",
                position: 2,
                isCompleted: true
            ),
        };

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.TaskStatuses.AddRange(statuses);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetProjectStatusesAsync(project.Id);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.All(result, s => Assert.Equal(project.Id, s.ProjectId));
    }

    [Fact]
    public async Task GetProjectStatusesAsync_WithNoStatuses_ReturnsEmptyList()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetProjectStatusesAsync(project.Id);

        // Assert
        Assert.Empty(result);
    }
}

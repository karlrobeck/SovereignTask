using TaskManagement;
using TaskManagement.Models;
using TaskManagement.Tests;
using Xunit;

namespace TaskManagement.Tests.Services;

[Collection("Database collection")]
public class TimeEntryServiceTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private TimeEntryService _service = null!;

    public TimeEntryServiceTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        // Clear database before each test for isolation
        await _fixture.ClearAsync();
        // Create a fresh service using the fixture's DbContext
        _service = new TimeEntryService(_fixture.DbContext);
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await Task.CompletedTask;
    }

    [Fact]
    public async Task CreateTimeEntryAsync_WithValidData_CreatesActiveTimer()
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

        var startTime = DateTime.UtcNow;

        // Act
        var result = await _service.CreateTimeEntryAsync(task.Id, user.Id, startTime);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(task.Id, result.TaskId);
        Assert.Equal(user.Id, result.UserId);
        Assert.Null(result.EndTime); // Timer is running
    }

    [Fact]
    public async Task GetActiveTimeEntryAsync_WithRunningTimer_ReturnsActiveEntry()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var user = TestDataBuilder.CreateUser(tenant.Id);
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var status = TestDataBuilder.CreateTaskStatus(project.Id);
        var task = TestDataBuilder.CreateTask(project.Id, status.Id);
        var activeEntry = TestDataBuilder.CreateTimeEntry(task.Id, user.Id, endTime: null);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Users.Add(user);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.TaskStatuses.Add(status);
        _fixture.DbContext.Tasks.Add(task);
        _fixture.DbContext.TimeEntries.Add(activeEntry);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetActiveTimeEntryAsync(user.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.EndTime);
        Assert.Equal(user.Id, result.UserId);
    }

    [Fact]
    public async Task StopTimerAsync_StopsRunningTimer()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var user = TestDataBuilder.CreateUser(tenant.Id);
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var status = TestDataBuilder.CreateTaskStatus(project.Id);
        var task = TestDataBuilder.CreateTask(project.Id, status.Id);
        var startTime = DateTime.UtcNow.AddHours(-1);
        var entry = new TimeEntryModel
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            UserId = user.Id,
            StartTime = startTime,
            EndTime = null, // Running
        };

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Users.Add(user);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.TaskStatuses.Add(status);
        _fixture.DbContext.Tasks.Add(task);
        _fixture.DbContext.TimeEntries.Add(entry);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.StopTimerAsync(entry.Id);

        // Assert
        Assert.NotNull(result.EndTime);
        Assert.True(result.EndTime > result.StartTime);
    }

    [Fact]
    public async Task GetTotalTimeTrackedAsync_AggregatesDuration()
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

        var entries = new[]
        {
            TestDataBuilder.CreateTimeEntry(
                task.Id,
                user.Id,
                DateTime.UtcNow.AddHours(-4),
                DateTime.UtcNow.AddHours(-3)
            ),
            TestDataBuilder.CreateTimeEntry(
                task.Id,
                user.Id,
                DateTime.UtcNow.AddHours(-2),
                DateTime.UtcNow.AddHours(-1)
            ),
        };
        _fixture.DbContext.TimeEntries.AddRange(entries);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetTotalTimeTrackedAsync(task.Id);

        // Assert
        Assert.Equal(2.0, result.TotalHours, precision: 2);
    }

    [Fact]
    public async Task GetUserTotalTimeAsync_AggregatesUserTime()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var user = TestDataBuilder.CreateUser(tenant.Id);
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var status = TestDataBuilder.CreateTaskStatus(project.Id);
        var task1 = TestDataBuilder.CreateTask(project.Id, status.Id);
        var task2 = TestDataBuilder.CreateTask(project.Id, status.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Users.Add(user);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.TaskStatuses.Add(status);
        _fixture.DbContext.Tasks.AddRange(task1, task2);

        var today = DateTime.UtcNow.Date;
        var entries = new[]
        {
            TestDataBuilder.CreateTimeEntry(
                task1.Id,
                user.Id,
                today.AddHours(8),
                today.AddHours(9)
            ),
            TestDataBuilder.CreateTimeEntry(
                task2.Id,
                user.Id,
                today.AddHours(10),
                today.AddHours(12)
            ),
        };
        _fixture.DbContext.TimeEntries.AddRange(entries);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetUserTotalTimeAsync(user.Id, today, today.AddDays(1));

        // Assert
        Assert.Equal(3, result.TotalHours); // 1 hour + 2 hours
    }

    [Fact]
    public async Task GetTimePerTaskAsync_BreaksDownTimeByTask()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var user = TestDataBuilder.CreateUser(tenant.Id);
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var status = TestDataBuilder.CreateTaskStatus(project.Id);
        var task1 = TestDataBuilder.CreateTask(project.Id, status.Id);
        var task2 = TestDataBuilder.CreateTask(project.Id, status.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Users.Add(user);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.TaskStatuses.Add(status);
        _fixture.DbContext.Tasks.AddRange(task1, task2);

        var today = DateTime.UtcNow.Date;
        var entries = new[]
        {
            TestDataBuilder.CreateTimeEntry(
                task1.Id,
                user.Id,
                today.AddHours(8),
                today.AddHours(10)
            ),
            TestDataBuilder.CreateTimeEntry(
                task1.Id,
                user.Id,
                today.AddHours(11),
                today.AddHours(12)
            ),
            TestDataBuilder.CreateTimeEntry(
                task2.Id,
                user.Id,
                today.AddHours(13),
                today.AddHours(14)
            ),
        };
        _fixture.DbContext.TimeEntries.AddRange(entries);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetTimePerTaskAsync(user.Id, today, today.AddDays(1));

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(3, result[task1.Id].TotalHours); // 2 + 1
        Assert.Equal(1, result[task2.Id].TotalHours);
    }

    [Fact]
    public async Task DeleteTimeEntryAsync_RemovesEntry()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var user = TestDataBuilder.CreateUser(tenant.Id);
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var status = TestDataBuilder.CreateTaskStatus(project.Id);
        var task = TestDataBuilder.CreateTask(project.Id, status.Id);
        var entry = TestDataBuilder.CreateTimeEntry(task.Id, user.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Users.Add(user);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.TaskStatuses.Add(status);
        _fixture.DbContext.Tasks.Add(task);
        _fixture.DbContext.TimeEntries.Add(entry);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        await _service.DeleteTimeEntryAsync(entry.Id);

        // Assert
        var result = await _service.GetTimeEntryByIdAsync(entry.Id);
        Assert.Null(result);
    }
}

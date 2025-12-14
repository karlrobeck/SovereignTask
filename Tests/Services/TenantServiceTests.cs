using TaskManagement;
using TaskManagement.Models;
using TaskManagement.Tests;
using Xunit;

namespace TaskManagement.Tests.Services;

/// <summary>
/// Integration tests for TenantService using xUnit and in-memory EF Core.
///
/// Key patterns demonstrated:
/// - Using DatabaseFixture for clean database per test
/// - Testing CRUD operations
/// - Testing business logic
/// - Verifying error handling
/// </summary>
[Collection("Database collection")]
public class TenantServiceTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private TenantService _service = null!;

    public TenantServiceTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        // Clear database before each test for isolation
        await _fixture.ClearAsync();
        // Create a fresh service using the fixture's DbContext
        _service = new TenantService(_fixture.DbContext);
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await Task.CompletedTask;
    }

    [Fact]
    public async Task CreateTenantAsync_WithValidData_CreatesAndReturnsTenant()
    {
        // Arrange
        var name = "Acme Corporation";
        var subscriptionStatus = "active";

        // Act
        var result = await _service.CreateTenantAsync(name, subscriptionStatus);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(name, result.Name);
        Assert.Equal(subscriptionStatus, result.SubscriptionStatus);
        Assert.NotEqual(DateTime.MinValue, result.CreatedAt);
    }

    [Fact]
    public async Task GetTenantByIdAsync_WithExistingTenant_ReturnsTenant()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        _fixture.DbContext.Tenants.Add(tenant);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetTenantByIdAsync(tenant.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(tenant.Id, result.Id);
        Assert.Equal(tenant.Name, result.Name);
    }

    [Fact]
    public async Task GetTenantByIdAsync_WithNonExistentId_ReturnsNull()
    {
        // Act
        var result = await _service.GetTenantByIdAsync(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllTenantsAsync_WithMultipleTenants_ReturnsAllTenants()
    {
        // Arrange
        var tenants = new[]
        {
            TestDataBuilder.CreateTenant("Tenant 1"),
            TestDataBuilder.CreateTenant("Tenant 2"),
            TestDataBuilder.CreateTenant("Tenant 3"),
        };
        _fixture.DbContext.Tenants.AddRange(tenants);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetAllTenantsAsync();

        // Assert
        Assert.NotEmpty(result);
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task GetTenantsBySubscriptionStatusAsync_WithTrialStatus_ReturnsMatchingTenants()
    {
        // Arrange
        var activeTenants = new[]
        {
            TestDataBuilder.CreateTenant("Active 1", "active"),
            TestDataBuilder.CreateTenant("Active 2", "active"),
        };
        var trialTenant = TestDataBuilder.CreateTenant("Trial", "trial");

        _fixture.DbContext.Tenants.AddRange(activeTenants);
        _fixture.DbContext.Tenants.Add(trialTenant);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetTenantsBySubscriptionStatusAsync("trial");

        // Assert
        Assert.Single(result);
        Assert.Equal("Trial", result[0].Name);
    }

    [Fact]
    public async Task UpdateTenantAsync_WithValidData_UpdatesTenant()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        _fixture.DbContext.Tenants.Add(tenant);
        await _fixture.DbContext.SaveChangesAsync();

        var newName = "Updated Tenant Name";
        var newStatus = "past_due";

        // Act
        var result = await _service.UpdateTenantAsync(tenant.Id, newName, newStatus);

        // Assert
        Assert.Equal(newName, result.Name);
        Assert.Equal(newStatus, result.SubscriptionStatus);
        Assert.True(result.UpdatedAt >= tenant.UpdatedAt);
    }

    [Fact]
    public async Task UpdateTenantAsync_WithNonExistentId_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UpdateTenantAsync(Guid.NewGuid(), "New Name", "active")
        );
    }

    [Fact]
    public async Task DeleteTenantAsync_WithExistingTenant_RemovesTenant()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        _fixture.DbContext.Tenants.Add(tenant);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        await _service.DeleteTenantAsync(tenant.Id);

        // Assert
        var result = await _service.GetTenantByIdAsync(tenant.Id);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetTenantUserCountAsync_WithMultipleUsers_ReturnsCorrectCount()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        _fixture.DbContext.Tenants.Add(tenant);
        await _fixture.DbContext.SaveChangesAsync();

        var users = new[]
        {
            TestDataBuilder.CreateUser(tenant.Id),
            TestDataBuilder.CreateUser(tenant.Id),
            TestDataBuilder.CreateUser(tenant.Id),
        };
        _fixture.DbContext.Users.AddRange(users);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var count = await _service.GetTenantUserCountAsync(tenant.Id);

        // Assert
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task GetTenantProjectCountAsync_WithMultipleProjects_ReturnsCorrectCount()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        _fixture.DbContext.Tenants.Add(tenant);
        await _fixture.DbContext.SaveChangesAsync();

        var projects = new[]
        {
            TestDataBuilder.CreateProject(tenant.Id, "Project 1"),
            TestDataBuilder.CreateProject(tenant.Id, "Project 2"),
        };
        _fixture.DbContext.Projects.AddRange(projects);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var count = await _service.GetTenantProjectCountAsync(tenant.Id);

        // Assert
        Assert.Equal(2, count);
    }
}

using TaskManagement;
using TaskManagement.Models;
using TaskManagement.Tests;
using Xunit;

namespace TaskManagement.Tests.Services;

[Collection("Database collection")]
public class MagicLinkServiceTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private MagicLinkService _service = null!;

    public MagicLinkServiceTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        // Clear database before each test for isolation
        await _fixture.ClearAsync();
        // Create a fresh service using the fixture's DbContext
        _service = new MagicLinkService(_fixture.DbContext);
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await Task.CompletedTask;
    }

    [Fact]
    public async Task CreateMagicLinkAsync_WithValidData_CreatesMagicLink()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var createdBy = TestDataBuilder.CreateUser(tenant.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.Users.Add(createdBy);
        await _fixture.DbContext.SaveChangesAsync();

        var expiresAt = DateTime.UtcNow.AddDays(30);
        var accessLevel = "read_only";

        // Act
        var result = await _service.CreateMagicLinkAsync(
            projectId: project.Id,
            createdById: createdBy.Id,
            accessLevel: accessLevel,
            expiresAt: expiresAt
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(project.Id, result.ProjectId);
        Assert.Equal(createdBy.Id, result.CreatedById);
        Assert.Equal(accessLevel, result.AccessLevel);
        Assert.NotNull(result.Token);
        Assert.True(result.Token.Length > 0);
    }

    [Fact]
    public async Task CreateMagicLinkAsync_GeneratesUniqueTokens()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var user = TestDataBuilder.CreateUser(tenant.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.Users.Add(user);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var link1 = await _service.CreateMagicLinkAsync(
            project.Id,
            user.Id,
            "read_only",
            DateTime.UtcNow.AddDays(30)
        );
        var link2 = await _service.CreateMagicLinkAsync(
            project.Id,
            user.Id,
            "read_only",
            DateTime.UtcNow.AddDays(30)
        );

        // Assert
        Assert.NotEqual(link1.Token, link2.Token);
    }

    [Fact]
    public async Task GetMagicLinkByIdAsync_WithExistingLink_ReturnsLink()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var user = TestDataBuilder.CreateUser(tenant.Id);
        var magicLink = TestDataBuilder.CreateMagicLink(project.Id, user.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.Users.Add(user);
        _fixture.DbContext.MagicLinks.Add(magicLink);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetMagicLinkByIdAsync(magicLink.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(magicLink.Id, result.Id);
        Assert.Equal(magicLink.Token, result.Token);
    }

    [Fact]
    public async Task GetMagicLinkByIdAsync_WithNonExistentId_ReturnsNull()
    {
        // Act
        var result = await _service.GetMagicLinkByIdAsync(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetMagicLinkByTokenAsync_WithExistingToken_ReturnsLink()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var user = TestDataBuilder.CreateUser(tenant.Id);
        var magicLink = TestDataBuilder.CreateMagicLink(project.Id, user.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.Users.Add(user);
        _fixture.DbContext.MagicLinks.Add(magicLink);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetMagicLinkByTokenAsync(magicLink.Token);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(magicLink.Id, result.Id);
        Assert.Equal(magicLink.Token, result.Token);
    }

    [Fact]
    public async Task GetMagicLinkByTokenAsync_WithNonExistentToken_ReturnsNull()
    {
        // Act
        var result = await _service.GetMagicLinkByTokenAsync("nonexistent-token");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetMagicLinksByProjectAsync_ReturnsAllProjectLinks()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var user = TestDataBuilder.CreateUser(tenant.Id);

        var magicLinks = new[]
        {
            TestDataBuilder.CreateMagicLink(project.Id, user.Id),
            TestDataBuilder.CreateMagicLink(project.Id, user.Id),
            TestDataBuilder.CreateMagicLink(project.Id, user.Id),
        };

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.Users.Add(user);
        _fixture.DbContext.MagicLinks.AddRange(magicLinks);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetMagicLinksByProjectAsync(project.Id);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.All(result, link => Assert.Equal(project.Id, link.ProjectId));
    }

    [Fact]
    public async Task GetMagicLinksByProjectAsync_WithNoLinks_ReturnsEmptyList()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetMagicLinksByProjectAsync(project.Id);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetActiveMagicLinksAsync_ReturnsOnlyNonExpiredLinks()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var user = TestDataBuilder.CreateUser(tenant.Id);

        var activeLink = new MagicLinkModel
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            CreatedById = user.Id,
            Token = Guid.NewGuid().ToString(),
            AccessLevel = "read_only",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
        };

        var expiredLink = new MagicLinkModel
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            CreatedById = user.Id,
            Token = Guid.NewGuid().ToString(),
            AccessLevel = "read_only",
            ExpiresAt = DateTime.UtcNow.AddDays(-1),
        };

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.Users.Add(user);
        _fixture.DbContext.MagicLinks.AddRange(activeLink, expiredLink);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetActiveMagicLinksAsync(project.Id);

        // Assert
        Assert.Single(result);
        Assert.Equal(activeLink.Id, result[0].Id);
    }

    [Fact]
    public async Task UpdateMagicLinkAsync_WithValidData_UpdatesLink()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var user = TestDataBuilder.CreateUser(tenant.Id);
        var magicLink = TestDataBuilder.CreateMagicLink(project.Id, user.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.Users.Add(user);
        _fixture.DbContext.MagicLinks.Add(magicLink);
        await _fixture.DbContext.SaveChangesAsync();

        var newAccessLevel = "comment_only";
        var newExpiresAt = DateTime.UtcNow.AddDays(60);

        // Act
        var result = await _service.UpdateMagicLinkAsync(
            linkId: magicLink.Id,
            accessLevel: newAccessLevel,
            expiresAt: newExpiresAt
        );

        // Assert
        Assert.Equal(newAccessLevel, result.AccessLevel);
        Assert.True((newExpiresAt - result.ExpiresAt).TotalSeconds < 1); // Allow small time difference
    }

    [Fact]
    public async Task UpdateMagicLinkAsync_WithNonExistentLink_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UpdateMagicLinkAsync(Guid.NewGuid(), accessLevel: "read_only")
        );
    }

    [Fact]
    public async Task DeleteMagicLinkAsync_RemovesLink()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var user = TestDataBuilder.CreateUser(tenant.Id);
        var magicLink = TestDataBuilder.CreateMagicLink(project.Id, user.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.Users.Add(user);
        _fixture.DbContext.MagicLinks.Add(magicLink);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        await _service.DeleteMagicLinkAsync(magicLink.Id);

        // Assert
        var result = await _service.GetMagicLinkByIdAsync(magicLink.Id);
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteMagicLinkAsync_WithNonExistentLink_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.DeleteMagicLinkAsync(Guid.NewGuid())
        );
    }

    [Fact]
    public async Task ValidateMagicLinkAsync_WithValidToken_ReturnsTrue()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var user = TestDataBuilder.CreateUser(tenant.Id);
        var magicLink = TestDataBuilder.CreateMagicLink(project.Id, user.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.Users.Add(user);
        _fixture.DbContext.MagicLinks.Add(magicLink);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.ValidateMagicLinkAsync(magicLink.Token);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ValidateMagicLinkAsync_WithInvalidToken_ReturnsFalse()
    {
        // Act
        var result = await _service.ValidateMagicLinkAsync("invalid-token-xyz");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetProjectByMagicLinkAsync_WithValidToken_ReturnsProject()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var user = TestDataBuilder.CreateUser(tenant.Id);
        var magicLink = TestDataBuilder.CreateMagicLink(project.Id, user.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.Users.Add(user);
        _fixture.DbContext.MagicLinks.Add(magicLink);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetProjectByMagicLinkAsync(magicLink.Token);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(project.Id, result.Id);
    }

    [Fact]
    public async Task GetProjectByMagicLinkAsync_WithInvalidToken_ReturnsNull()
    {
        // Act
        var result = await _service.GetProjectByMagicLinkAsync("invalid-token");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task RevokeMagicLinkAsync_SetsExpirationToNow()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var user = TestDataBuilder.CreateUser(tenant.Id);
        var magicLink = TestDataBuilder.CreateMagicLink(project.Id, user.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.Users.Add(user);
        _fixture.DbContext.MagicLinks.Add(magicLink);
        await _fixture.DbContext.SaveChangesAsync();

        var beforeRevoke = DateTime.UtcNow;

        // Act
        await _service.RevokeMagicLinkAsync(magicLink.Id);

        // Assert
        var result = await _service.GetMagicLinkByIdAsync(magicLink.Id);
        Assert.NotNull(result);
        Assert.True(result.ExpiresAt <= beforeRevoke.AddSeconds(1));
    }

    [Fact]
    public async Task RevokeMagicLinkAsync_WithNonExistentLink_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.RevokeMagicLinkAsync(Guid.NewGuid())
        );
    }

    [Fact]
    public async Task DeleteExpiredLinksAsync_RemovesExpiredLinks()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var user = TestDataBuilder.CreateUser(tenant.Id);

        var expiredLink = new MagicLinkModel
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            CreatedById = user.Id,
            Token = Guid.NewGuid().ToString(),
            AccessLevel = "read_only",
            ExpiresAt = DateTime.UtcNow.AddDays(-1),
        };

        var activeLink = new MagicLinkModel
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            CreatedById = user.Id,
            Token = Guid.NewGuid().ToString(),
            AccessLevel = "read_only",
            ExpiresAt = DateTime.UtcNow.AddDays(30),
        };

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.Users.Add(user);
        _fixture.DbContext.MagicLinks.AddRange(expiredLink, activeLink);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var deletedCount = await _service.DeleteExpiredLinksAsync();

        // Assert
        Assert.Equal(1, deletedCount);
        var remaining = await _service.GetMagicLinksByProjectAsync(project.Id);
        Assert.Single(remaining);
        Assert.Equal(activeLink.Id, remaining[0].Id);
    }

    [Fact]
    public async Task DeleteExpiredLinksAsync_WithNoExpiredLinks_ReturnsZero()
    {
        // Arrange
        var tenant = TestDataBuilder.CreateTenant();
        var project = TestDataBuilder.CreateProject(tenant.Id);
        var user = TestDataBuilder.CreateUser(tenant.Id);
        var magicLink = TestDataBuilder.CreateMagicLink(project.Id, user.Id);

        _fixture.DbContext.Tenants.Add(tenant);
        _fixture.DbContext.Projects.Add(project);
        _fixture.DbContext.Users.Add(user);
        _fixture.DbContext.MagicLinks.Add(magicLink);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var deletedCount = await _service.DeleteExpiredLinksAsync();

        // Assert
        Assert.Equal(0, deletedCount);
    }
}

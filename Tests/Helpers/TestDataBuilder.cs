using TaskManagement;
using TaskManagement.Models;

namespace TaskManagement.Tests;

/// <summary>
/// Helper class to seed test data into the database.
/// Reduces boilerplate in tests by providing factory methods.
/// </summary>
public static class TestDataBuilder
{
    public static TenantModel CreateTenant(
        string name = "Test Tenant",
        string subscriptionStatus = "active"
    )
    {
        return new TenantModel
        {
            Id = Guid.NewGuid(),
            Name = name,
            SubscriptionStatus = subscriptionStatus,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    public static UserModel CreateUser(
        Guid tenantId,
        string? email = null,
        string displayName = "Test User",
        string role = "member",
        string? entraOid = null
    )
    {
        // Generate unique email if not provided
        email ??= $"user_{Guid.NewGuid().ToString().Substring(0, 8)}@example.com";

        return new UserModel
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = email,
            DisplayName = displayName,
            Role = role,
            EntraOid = entraOid,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    public static ProjectModel CreateProject(
        Guid tenantId,
        string name = "Test Project",
        string keyPrefix = "TEST"
    )
    {
        return new ProjectModel
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            KeyPrefix = keyPrefix,
            Description = "Test project description",
            IsArchived = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    public static TaskStatusModel CreateTaskStatus(
        Guid projectId,
        string name = "To Do",
        int position = 0,
        bool isCompleted = false
    )
    {
        return new TaskStatusModel
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = name,
            Position = position,
            IsCompleted = isCompleted,
        };
    }

    public static TaskModel CreateTask(
        Guid projectId,
        Guid statusId,
        string title = "Test Task",
        string? description = null,
        Guid? parentId = null,
        Guid? assigneeId = null,
        int priority = 0
    )
    {
        return new TaskModel
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            StatusId = statusId,
            Title = title,
            Description = description,
            ParentId = parentId,
            AssigneeId = assigneeId,
            Priority = priority,
            StartDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(7),
            EstimatedMinutes = 480,
            RowVersion = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    public static TimeEntryModel CreateTimeEntry(
        Guid taskId,
        Guid userId,
        DateTime? startTime = null,
        DateTime? endTime = null,
        string? description = null
    )
    {
        startTime ??= DateTime.UtcNow.AddHours(-2);
        // Only set default endTime if not explicitly provided and not null
        // If caller wants an active timer, they must explicitly pass a time or keep as default
        // To create active timer, pass endTime: null explicitly before this line runs

        return new TimeEntryModel
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            UserId = userId,
            StartTime = startTime.Value,
            EndTime = endTime,
            Description = description ?? "Test time entry",
        };
    }

    public static MagicLinkModel CreateMagicLink(
        Guid projectId,
        Guid createdById,
        string accessLevel = "read_only"
    )
    {
        return new MagicLinkModel
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            CreatedById = createdById,
            AccessLevel = accessLevel,
            Token = Guid.NewGuid().ToString("N"),
            ExpiresAt = DateTime.UtcNow.AddDays(30),
        };
    }
}

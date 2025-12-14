namespace TaskManagement;

public class ProjectModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string Name { get; set; }
    public required string KeyPrefix { get; set; } // Short code like "WEB" for WEB-101
    public string? Description { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public TenantModel? Tenant { get; set; }
    public ICollection<TaskStatusModel> TaskStatuses { get; } = [];
    public ICollection<TaskModel> Tasks { get; } = [];
    public ICollection<MagicLinkModel> MagicLinks { get; } = [];
}

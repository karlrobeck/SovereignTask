namespace TaskManagement;

public class UserModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string? EntraOid { get; set; } // Microsoft Entra ID Object ID
    public required string Email { get; set; }
    public required string DisplayName { get; set; }
    public required string Role { get; set; } // 'admin', 'member', 'guest'
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public TenantModel? Tenant { get; set; }
    public ICollection<TaskModel> AssignedTasks { get; } = [];
    public ICollection<TimeEntryModel> TimeEntries { get; } = [];
    public ICollection<MagicLinkModel> CreatedMagicLinks { get; } = [];
    public ICollection<AuditLogModel> AuditLogs { get; } = [];
}

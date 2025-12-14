namespace TaskManagement;

public class AuditLogModel
{
    public long Id { get; set; } // Auto-incrementing
    public Guid TenantId { get; set; }
    public required string EntityTable { get; set; } // e.g., 'tasks', 'projects'
    public Guid EntityId { get; set; }
    public required string Action { get; set; } // 'CREATE', 'UPDATE', 'DELETE'
    public Guid ChangedById { get; set; }
    public string? ChangesJson { get; set; } // Snapshot of OldValue/NewValue
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public TenantModel? Tenant { get; set; }
    public UserModel? ChangedBy { get; set; }
}

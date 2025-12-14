namespace TaskManagement;

public class TenantModel
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string SubscriptionStatus { get; set; } // 'active', 'trial', 'past_due'
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public ICollection<UserModel> Users { get; } = [];
    public ICollection<ProjectModel> Projects { get; } = [];
    public ICollection<AuditLogModel> AuditLogs { get; } = [];
}

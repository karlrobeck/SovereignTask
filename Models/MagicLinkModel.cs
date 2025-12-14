namespace TaskManagement;

public class MagicLinkModel
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public required string Token { get; set; } // Cryptographically secure token
    public required string AccessLevel { get; set; } // 'read_only' or 'comment_only'
    public DateTime ExpiresAt { get; set; }
    public Guid CreatedById { get; set; }

    // Navigation properties
    public ProjectModel? Project { get; set; }
    public UserModel? CreatedBy { get; set; }
}

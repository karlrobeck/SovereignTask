namespace TaskManagement;

public class TaskModel
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ParentId { get; set; } // Recursive self-reference for subtasks
    public Guid StatusId { get; set; }
    public Guid? AssigneeId { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public int Priority { get; set; } // 0=Low, 1=Medium, 2=High, 3=Critical
    public DateTime? StartDate { get; set; }
    public DateTime? DueDate { get; set; }
    public int EstimatedMinutes { get; set; }
    public int RowVersion { get; set; } = 1; // Optimistic concurrency token
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public ProjectModel? Project { get; set; }
    public TaskStatusModel? Status { get; set; }
    public UserModel? Assignee { get; set; }
    public TaskModel? Parent { get; set; }
    public ICollection<TaskModel> Subtasks { get; } = [];
    public ICollection<TaskDependencyModel> PredecessorDependencies { get; } = []; // Tasks that depend on this
    public ICollection<TaskDependencyModel> SuccessorDependencies { get; } = []; // Tasks this depends on
    public ICollection<TimeEntryModel> TimeEntries { get; } = [];
    public ICollection<AuditLogModel> AuditLogs { get; } = [];
}

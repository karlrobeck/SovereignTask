namespace TaskManagement;

public class TimeEntryModel
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public Guid UserId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; } // NULL = currently running
    public string? Description { get; set; }

    // Navigation properties
    public TaskModel? Task { get; set; }
    public UserModel? User { get; set; }
}

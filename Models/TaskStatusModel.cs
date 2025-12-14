namespace TaskManagement;

public class TaskStatusModel
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public required string Name { get; set; } // e.g., "To Do", "In Progress", "QA"
    public int Position { get; set; } // Ordering on Kanban board
    public bool IsCompleted { get; set; } // Flags if status represents "Done"

    // Navigation properties
    public ProjectModel? Project { get; set; }
    public ICollection<TaskModel> Tasks { get; } = [];
}

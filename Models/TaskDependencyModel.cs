namespace TaskManagement;

public class TaskDependencyModel
{
    public Guid Id { get; set; }
    public Guid PredecessorId { get; set; }
    public Guid SuccessorId { get; set; }
    public string Type { get; set; } = "FS"; // Finish-to-Start

    // Navigation properties
    public TaskModel? Predecessor { get; set; }
    public TaskModel? Successor { get; set; }
}

using Microsoft.EntityFrameworkCore;

namespace TaskManagement;

public class Context : DbContext
{
    public DbSet<TenantModel> Tenants { get; set; }
    public DbSet<UserModel> Users { get; set; }
    public DbSet<ProjectModel> Projects { get; set; }
    public DbSet<TaskStatusModel> TaskStatuses { get; set; }
    public DbSet<TaskModel> Tasks { get; set; }
    public DbSet<TaskDependencyModel> TaskDependencies { get; set; }
    public DbSet<TimeEntryModel> TimeEntries { get; set; }
    public DbSet<MagicLinkModel> MagicLinks { get; set; }
    public DbSet<AuditLogModel> AuditLogs { get; set; }

    public string DbPath { get; }

    public Context(DbContextOptions<Context> options)
        : base(options)
    {
        var folder = Environment.SpecialFolder.LocalApplicationData;
        var path = Environment.GetFolderPath(folder);
        DbPath = Path.Join(path, "task.db");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Only configure SQLite if no provider is already configured (e.g., from tests using InMemory)
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlite($"Data Source={DbPath}");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure TenantModel
        modelBuilder.Entity<TenantModel>().HasKey(t => t.Id);
        modelBuilder
            .Entity<TenantModel>()
            .HasMany(t => t.Users)
            .WithOne(u => u.Tenant)
            .HasForeignKey(u => u.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder
            .Entity<TenantModel>()
            .HasMany(t => t.Projects)
            .WithOne(p => p.Tenant)
            .HasForeignKey(p => p.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder
            .Entity<TenantModel>()
            .HasMany(t => t.AuditLogs)
            .WithOne(a => a.Tenant)
            .HasForeignKey(a => a.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure UserModel
        modelBuilder.Entity<UserModel>().HasKey(u => u.Id);
        modelBuilder.Entity<UserModel>().HasIndex(u => u.Email).IsUnique();
        modelBuilder
            .Entity<UserModel>()
            .HasIndex(u => new { u.TenantId, u.EntraOid })
            .IsUnique()
            .HasFilter("[EntraOid] IS NOT NULL"); // Only unique when EntraOid is not null
        modelBuilder
            .Entity<UserModel>()
            .HasMany(u => u.AssignedTasks)
            .WithOne(t => t.Assignee)
            .HasForeignKey(t => t.AssigneeId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder
            .Entity<UserModel>()
            .HasMany(u => u.TimeEntries)
            .WithOne(t => t.User)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder
            .Entity<UserModel>()
            .HasMany(u => u.CreatedMagicLinks)
            .WithOne(m => m.CreatedBy)
            .HasForeignKey(m => m.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder
            .Entity<UserModel>()
            .HasMany(u => u.AuditLogs)
            .WithOne(a => a.ChangedBy)
            .HasForeignKey(a => a.ChangedById)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure ProjectModel
        modelBuilder.Entity<ProjectModel>().HasKey(p => p.Id);
        modelBuilder
            .Entity<ProjectModel>()
            .HasMany(p => p.TaskStatuses)
            .WithOne(s => s.Project)
            .HasForeignKey(s => s.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder
            .Entity<ProjectModel>()
            .HasMany(p => p.Tasks)
            .WithOne(t => t.Project)
            .HasForeignKey(t => t.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder
            .Entity<ProjectModel>()
            .HasMany(p => p.MagicLinks)
            .WithOne(m => m.Project)
            .HasForeignKey(m => m.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure TaskStatusModel
        modelBuilder.Entity<TaskStatusModel>().HasKey(s => s.Id);
        modelBuilder
            .Entity<TaskStatusModel>()
            .HasMany(s => s.Tasks)
            .WithOne(t => t.Status)
            .HasForeignKey(t => t.StatusId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure TaskModel
        modelBuilder.Entity<TaskModel>().HasKey(t => t.Id);
        modelBuilder
            .Entity<TaskModel>()
            .HasOne(t => t.Parent)
            .WithMany(t => t.Subtasks)
            .HasForeignKey(t => t.ParentId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder
            .Entity<TaskModel>()
            .HasMany(t => t.PredecessorDependencies)
            .WithOne(d => d.Predecessor)
            .HasForeignKey(d => d.PredecessorId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder
            .Entity<TaskModel>()
            .HasMany(t => t.SuccessorDependencies)
            .WithOne(d => d.Successor)
            .HasForeignKey(d => d.SuccessorId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder
            .Entity<TaskModel>()
            .HasMany(t => t.TimeEntries)
            .WithOne(t => t.Task)
            .HasForeignKey(t => t.TaskId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder
            .Entity<TaskModel>()
            .HasMany(t => t.AuditLogs)
            .WithOne()
            .OnDelete(DeleteBehavior.Cascade);

        // Configure TaskDependencyModel
        modelBuilder.Entity<TaskDependencyModel>().HasKey(d => d.Id);
        modelBuilder
            .Entity<TaskDependencyModel>()
            .HasIndex(d => d.PredecessorId)
            .HasDatabaseName("idx_deps_predecessor");

        // Configure TimeEntryModel
        modelBuilder.Entity<TimeEntryModel>().HasKey(t => t.Id);
        modelBuilder
            .Entity<TimeEntryModel>()
            .HasIndex(t => new { t.UserId })
            .HasFilter("\"EndTime\" IS NULL")
            .HasDatabaseName("idx_time_entries_active");

        // Configure MagicLinkModel
        modelBuilder.Entity<MagicLinkModel>().HasKey(m => m.Id);
        modelBuilder.Entity<MagicLinkModel>().HasIndex(m => m.Token).IsUnique();

        // Configure AuditLogModel
        modelBuilder.Entity<AuditLogModel>().HasKey(a => a.Id);
        modelBuilder.Entity<AuditLogModel>().Property(a => a.Id).ValueGeneratedOnAdd();

        // Performance indexes
        modelBuilder
            .Entity<TaskModel>()
            .HasIndex(t => new { t.ProjectId, t.StatusId })
            .HasDatabaseName("idx_tasks_project_status");
        modelBuilder
            .Entity<TaskModel>()
            .HasIndex(t => t.ParentId)
            .HasDatabaseName("idx_tasks_parent");
    }
}

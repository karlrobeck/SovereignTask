using Microsoft.EntityFrameworkCore;

namespace TaskManagement;

public class UserService
{
    private readonly Context _dbContext;

    public UserService(Context dbContext)
    {
        _dbContext = dbContext;
    }

    // CREATE
    public async Task<UserModel> CreateUserAsync(
        Guid tenantId,
        string email,
        string displayName,
        string role,
        string? entraOid = null
    )
    {
        var user = new UserModel
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = email,
            DisplayName = displayName,
            Role = role,
            EntraOid = entraOid,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _dbContext.Users.Add(user);

        try
        {
            await _dbContext.SaveChangesAsync();
            return user;
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("Failed to create user", ex);
        }
    }

    // READ
    public async Task<UserModel?> GetUserByIdAsync(Guid userId)
    {
        return await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
    }

    public async Task<UserModel?> GetUserByEmailAsync(Guid tenantId, string email)
    {
        return await _dbContext
            .Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Email == email);
    }

    public async Task<UserModel?> GetUserByEntraOidAsync(Guid tenantId, string entraOid)
    {
        return await _dbContext
            .Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.EntraOid == entraOid);
    }

    public async Task<List<UserModel>> GetUsersByTenantAsync(Guid tenantId)
    {
        return await _dbContext
            .Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId)
            .ToListAsync();
    }

    public async Task<List<UserModel>> GetUsersByRoleAsync(Guid tenantId, string role)
    {
        return await _dbContext
            .Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.Role == role)
            .ToListAsync();
    }

    // UPDATE
    public async Task<UserModel> UpdateUserAsync(
        Guid userId,
        string? email = null,
        string? displayName = null,
        string? role = null
    )
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            throw new InvalidOperationException($"User with ID {userId} not found");

        if (!string.IsNullOrEmpty(email))
            user.Email = email;
        if (!string.IsNullOrEmpty(displayName))
            user.DisplayName = displayName;
        if (!string.IsNullOrEmpty(role))
            user.Role = role;

        user.UpdatedAt = DateTime.UtcNow;
        _dbContext.Users.Update(user);

        try
        {
            await _dbContext.SaveChangesAsync();
            return user;
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("Failed to update user", ex);
        }
    }

    // DELETE
    public async Task DeleteUserAsync(Guid userId)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            throw new InvalidOperationException($"User with ID {userId} not found");

        _dbContext.Users.Remove(user);

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("Failed to delete user", ex);
        }
    }

    // ADDITIONAL
    public async Task<List<TimeEntryModel>> GetUserTimeEntriesAsync(Guid userId)
    {
        return await _dbContext
            .TimeEntries.AsNoTracking()
            .Where(t => t.UserId == userId)
            .ToListAsync();
    }

    public async Task<List<TaskModel>> GetUserAssignedTasksAsync(Guid userId)
    {
        return await _dbContext
            .Tasks.AsNoTracking()
            .Where(t => t.AssigneeId == userId)
            .ToListAsync();
    }
}

using Microsoft.EntityFrameworkCore;

namespace TaskManagement;

public class TenantService
{
    private readonly Context _dbContext;

    public TenantService(Context dbContext)
    {
        _dbContext = dbContext;
    }

    // CREATE
    public async Task<TenantModel> CreateTenantAsync(string name, string subscriptionStatus)
    {
        var tenant = new TenantModel
        {
            Id = Guid.NewGuid(),
            Name = name,
            SubscriptionStatus = subscriptionStatus,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _dbContext.Tenants.Add(tenant);

        try
        {
            await _dbContext.SaveChangesAsync();
            return tenant;
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("Failed to create tenant", ex);
        }
    }

    // READ
    public async Task<TenantModel?> GetTenantByIdAsync(Guid tenantId)
    {
        return await _dbContext.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId);
    }

    public async Task<List<TenantModel>> GetAllTenantsAsync()
    {
        return await _dbContext.Tenants.AsNoTracking().ToListAsync();
    }

    public async Task<List<TenantModel>> GetTenantsBySubscriptionStatusAsync(
        string subscriptionStatus
    )
    {
        return await _dbContext
            .Tenants.AsNoTracking()
            .Where(t => t.SubscriptionStatus == subscriptionStatus)
            .ToListAsync();
    }

    // UPDATE
    public async Task<TenantModel> UpdateTenantAsync(
        Guid tenantId,
        string name,
        string subscriptionStatus
    )
    {
        var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId);
        if (tenant == null)
            throw new InvalidOperationException($"Tenant with ID {tenantId} not found");

        tenant.Name = name;
        tenant.SubscriptionStatus = subscriptionStatus;
        tenant.UpdatedAt = DateTime.UtcNow;

        _dbContext.Tenants.Update(tenant);

        try
        {
            await _dbContext.SaveChangesAsync();
            return tenant;
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("Failed to update tenant", ex);
        }
    }

    // DELETE
    public async Task DeleteTenantAsync(Guid tenantId)
    {
        var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId);
        if (tenant == null)
            throw new InvalidOperationException($"Tenant with ID {tenantId} not found");

        _dbContext.Tenants.Remove(tenant);

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("Failed to delete tenant", ex);
        }
    }

    // ADDITIONAL
    public async Task<int> GetTenantUserCountAsync(Guid tenantId)
    {
        return await _dbContext.Users.Where(u => u.TenantId == tenantId).CountAsync();
    }

    public async Task<int> GetTenantProjectCountAsync(Guid tenantId)
    {
        return await _dbContext.Projects.Where(p => p.TenantId == tenantId).CountAsync();
    }
}

using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;

namespace TaskManagement;

public class MagicLinkService(Context dbContext)
{
    private readonly Context _dbContext = dbContext;

    // CREATE
    public async Task<MagicLinkModel> CreateMagicLinkAsync(
        Guid projectId,
        Guid createdById,
        string accessLevel,
        DateTime expiresAt
    )
    {
        // Generate a cryptographically secure random token
        var tokenBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(tokenBytes);
        }
        var token = Convert
            .ToBase64String(tokenBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

        var magicLink = new MagicLinkModel
        {
            ProjectId = projectId,
            AccessLevel = accessLevel,
            CreatedById = createdById,
            ExpiresAt = expiresAt,
            Token = token,
        };

        _dbContext.MagicLinks.Add(magicLink);

        try
        {
            await _dbContext.SaveChangesAsync();
            return magicLink;
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException($"Failed to create magic link", ex);
        }
    }

    // READ
    public async Task<MagicLinkModel?> GetMagicLinkByIdAsync(Guid linkId)
    {
        return await _dbContext.MagicLinks.AsNoTracking().FirstOrDefaultAsync(a => a.Id == linkId);
    }

    public async Task<MagicLinkModel?> GetMagicLinkByTokenAsync(string token)
    {
        return await _dbContext
            .MagicLinks.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Token == token);
    }

    public async Task<List<MagicLinkModel>> GetMagicLinksByProjectAsync(Guid projectId)
    {
        return await _dbContext
            .MagicLinks.AsNoTracking()
            .Where(a => a.ProjectId == projectId)
            .ToListAsync();
    }

    public async Task<List<MagicLinkModel>> GetActiveMagicLinksAsync(Guid projectId)
    {
        var currentDate = DateTime.UtcNow;

        return await _dbContext
            .MagicLinks.AsNoTracking()
            .Where(a => a.ProjectId == projectId)
            .Where(a => a.ExpiresAt > currentDate)
            .ToListAsync();
    }

    // UPDATE
    public async Task<MagicLinkModel> UpdateMagicLinkAsync(
        Guid linkId,
        string? accessLevel = null,
        DateTime? expiresAt = null
    )
    {
        var magicLink =
            await _dbContext.MagicLinks.FirstOrDefaultAsync(a => a.Id == linkId)
            ?? throw new InvalidOperationException($"Magic link with ID {linkId} not found");

        if (accessLevel != null)
        {
            magicLink.AccessLevel = accessLevel;
        }

        if (expiresAt != null)
        {
            magicLink.ExpiresAt = (DateTime)expiresAt;
        }

        _dbContext.MagicLinks.Update(magicLink);

        try
        {
            await _dbContext.SaveChangesAsync();

            return magicLink;
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("Failed to update magic link", ex);
        }
    }

    // DELETE
    public async Task DeleteMagicLinkAsync(Guid linkId)
    {
        var magicLink =
            await _dbContext.MagicLinks.FirstOrDefaultAsync(a => a.Id == linkId)
            ?? throw new InvalidOperationException($"Magic link with ID {linkId} not found");

        _dbContext.Remove(magicLink);

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("Failed to remove magic link", ex);
        }
    }

    // ADDITIONAL
    public async Task<bool> ValidateMagicLinkAsync(string token)
    {
        return await _dbContext.MagicLinks.AnyAsync(a => a.Token == token);
    }

    public async Task<ProjectModel?> GetProjectByMagicLinkAsync(string token)
    {
        return await _dbContext
            .Projects.AsNoTracking()
            .Include(a => a.MagicLinks)
            .FirstOrDefaultAsync(p => p.MagicLinks.Any(m => m.Token == token));
    }

    public async Task RevokeMagicLinkAsync(Guid linkId)
    {
        var magicLink =
            await _dbContext.MagicLinks.FirstOrDefaultAsync(a => a.Id == linkId)
            ?? throw new InvalidOperationException($"Magic link with ID {linkId} not found");

        magicLink.ExpiresAt = DateTime.UtcNow;
        _dbContext.MagicLinks.Update(magicLink);

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("Failed to revoke magic link", ex);
        }
    }

    public async Task<int> DeleteExpiredLinksAsync()
    {
        var expiredLinks = await _dbContext
            .MagicLinks.Where(a => a.ExpiresAt <= DateTime.UtcNow)
            .ToListAsync();

        if (expiredLinks.Count == 0)
            return 0;

        _dbContext.MagicLinks.RemoveRange(expiredLinks);

        try
        {
            await _dbContext.SaveChangesAsync();
            return expiredLinks.Count;
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException(
                $"Failed to delete {expiredLinks.Count} expired magic links",
                ex
            );
        }
    }
}

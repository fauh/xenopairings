using Microsoft.EntityFrameworkCore;
using Xenopairings.Data;
using Xenopairings.Models;

namespace Xenopairings.Services.Organizations;

public sealed class OrganizationService(AppDbContext db, TokenGenerator tokenGenerator) : IOrganizationService
{
    public async Task<Organization> CreateAsync(string name, Guid createdByUserId)
    {
        var org = new Organization
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            InviteToken = tokenGenerator.RandomUrlSafeString(12),
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Organizations.Add(org);

        // Creator automatically becomes a member
        db.OrganizationMembers.Add(new OrganizationMember
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            UserId = createdByUserId,
            JoinedAt = DateTimeOffset.UtcNow,
        });

        await db.SaveChangesAsync();
        return org;
    }

    public Task<Organization?> GetByIdAsync(Guid id) =>
        db.Organizations
            .Include(o => o.Members).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(o => o.Id == id);

    public Task<Organization?> GetByInviteTokenAsync(string token) =>
        db.Organizations
            .Include(o => o.Members).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(o => o.InviteToken == token);

    public async Task<IReadOnlyList<Organization>> ListByUserAsync(Guid userId)
    {
        var orgs = await db.OrganizationMembers
            .Where(m => m.UserId == userId)
            .Include(m => m.Organization).ThenInclude(o => o.Members).ThenInclude(m => m.User)
            .Select(m => m.Organization)
            .ToListAsync();
        return [.. orgs.OrderBy(o => o.Name)];
    }

    public async Task JoinAsync(Guid organizationId, Guid userId)
    {
        var alreadyMember = await db.OrganizationMembers
            .AnyAsync(m => m.OrganizationId == organizationId && m.UserId == userId);
        if (alreadyMember) return;

        db.OrganizationMembers.Add(new OrganizationMember
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = userId,
            JoinedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    public async Task LeaveAsync(Guid organizationId, Guid userId)
    {
        var member = await db.OrganizationMembers
            .FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == userId);
        if (member is null) return;
        db.OrganizationMembers.Remove(member);
        await db.SaveChangesAsync();
    }
}

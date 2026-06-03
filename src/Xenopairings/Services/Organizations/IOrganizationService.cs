using Xenopairings.Models;

namespace Xenopairings.Services.Organizations;

public interface IOrganizationService
{
    Task<Organization> CreateAsync(string name, Guid createdByUserId);
    Task<Organization?> GetByIdAsync(Guid id);
    Task<Organization?> GetByInviteTokenAsync(string token);
    /// <summary>Returns all organizations the user belongs to (as creator or member), with Members loaded.</summary>
    Task<IReadOnlyList<Organization>> ListByUserAsync(Guid userId);
    /// <summary>Adds the user to the organization. No-op if already a member.</summary>
    Task JoinAsync(Guid organizationId, Guid userId);
    Task LeaveAsync(Guid organizationId, Guid userId);
}

namespace Xenopairings.Models;

public class Organization
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Short URL-safe token shared to invite new members.</summary>
    public string InviteToken { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<OrganizationMember> Members { get; set; } = [];
}

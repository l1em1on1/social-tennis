namespace SocialTennis.Api.Domain;

/// <summary>
/// A User who runs one League (#220; CONTEXT.md, League manager): assigned to it
/// by a Club manager, or who created it. Any number per League. Deleted with
/// either. A Club's manager and an Admin need none, and a League with none of
/// its own is run by them.
/// </summary>
public class LeagueManager
{
    public Guid LeagueId { get; set; }

    public League League { get; set; } = null!;

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public DateTimeOffset AssignedAt { get; set; }
}

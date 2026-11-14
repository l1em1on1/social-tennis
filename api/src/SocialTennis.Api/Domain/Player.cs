namespace SocialTennis.Api.Domain;

/// <summary>
/// A tennis profile held by a User (see CONTEXT.md). A User may hold several —
/// a parent and their children — and switches between them.
/// </summary>
/// <remarks>
/// Only the Name so far. Gender, Level, Player Rating and Players a Manager
/// creates before their holder has logged in are the rest of issue #4.
/// </remarks>
public class Player
{
    public const int NameMaxLength = 100;

    public Guid Id { get; set; }

    public required string Name { get; set; }

    /// <summary>The User holding this Player.</summary>
    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public List<ClubMembership> Memberships { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }
}

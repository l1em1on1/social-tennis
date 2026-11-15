namespace SocialTennis.Api.Domain;

/// <summary>
/// A tennis profile held by a User (see CONTEXT.md). A User may hold several —
/// a parent and their children — and switches between them.
/// </summary>
/// <remarks>
/// A Manager may add a Player for an email before that person has ever signed
/// in. It is held by the User for that email all the same: a User row exists
/// for any address a sign-in link was asked for or a Player was added for, and
/// only someone who can read that address's mail can sign in as it.
/// </remarks>
public class Player
{
    public const int NameMaxLength = 100;

    public const int MinLevel = 1;

    public const int MaxLevel = 5;

    /// <summary>Every new Player's Rating: nobody is unrated (ADR-0007).</summary>
    public const int DefaultRating = 1;

    public const int MinRating = 1;

    public const int MaxRating = 10_000;

    public Guid Id { get; set; }

    public required string Name { get; set; }

    /// <summary>Not asked at first sign-in, so it may be unset.</summary>
    public PlayerGender? Gender { get; set; }

    /// <summary>
    /// The club's informal grouping, 1 to 5. Informational only: nothing
    /// computes from it, and it does not seed Rating (ADR-0007).
    /// </summary>
    public int? Level { get; set; }

    /// <summary>
    /// Player Rating: set by hand by a Manager, never by the holder, and never
    /// derived from Level or results (ADR-0007).
    /// </summary>
    public int Rating { get; set; } = DefaultRating;

    /// <summary>The User holding this Player.</summary>
    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public List<ClubMembership> Memberships { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }
}

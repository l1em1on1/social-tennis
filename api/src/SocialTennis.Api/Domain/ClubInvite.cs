namespace SocialTennis.Api.Domain;

/// <summary>
/// An email address a Manager has let into a Club ahead of time, waiting for
/// its User's first Player. It is used up (deleted) as soon as it lets a Player
/// in, so it exists only while that User holds no Player (#164).
/// </summary>
public class ClubInvite
{
    public Guid Id { get; set; }

    public Guid ClubId { get; set; }

    public Club Club { get; set; } = null!;

    /// <summary>Normalized (trimmed, lower-case), like <see cref="User.Email"/>.</summary>
    public required string Email { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

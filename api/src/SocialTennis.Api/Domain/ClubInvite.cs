namespace SocialTennis.Api.Domain;

/// <summary>
/// An email address a Manager has let into a Club ahead of time. Every Player
/// held by the User with that email is a member without asking: Players they
/// already have when the invite is made, and Players they add later.
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

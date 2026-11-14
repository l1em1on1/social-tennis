namespace SocialTennis.Api.Domain;

public enum ClubMembershipStatus
{
    /// <summary>The Player asked to join; a Manager hasn't approved yet.</summary>
    Pending = 0,

    /// <summary>A member: approved by a Manager, or invited by email beforehand.</summary>
    Active = 1,
}

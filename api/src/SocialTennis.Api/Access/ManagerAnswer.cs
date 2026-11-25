namespace SocialTennis.Api.Access;

/// <summary>What the rule says to a User about one Club, League, Game or Player.</summary>
public enum ManagerAnswer
{
    Allowed,

    /// <summary>Not theirs to manage, but they can see it: a 403.</summary>
    Refused,

    /// <summary>Not theirs to see, or not there at all: a 404 either way, so neither can be told from the other.</summary>
    Hidden,
}

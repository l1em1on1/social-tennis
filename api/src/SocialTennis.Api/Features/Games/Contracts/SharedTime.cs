namespace SocialTennis.Api.Features.Games.Contracts;

/// <summary>A day every Player in a Game is free, with the time they all share (#276).</summary>
/// <param name="Date">The day.</param>
/// <param name="From">From when everyone is free, with To; neither is any time.</param>
/// <param name="To">Until when everyone is free.</param>
public record SharedTime(DateOnly Date, TimeOnly? From, TimeOnly? To);

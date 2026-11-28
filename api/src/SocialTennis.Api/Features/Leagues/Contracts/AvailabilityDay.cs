namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>What counts on one date of a Player's Stage (#276): their own setting for it, else their usual week.</summary>
/// <param name="Date">The date.</param>
/// <param name="Own">Whether they set this date themselves; otherwise it follows their usual week.</param>
/// <param name="Free">Whether they're free that day.</param>
/// <param name="From">From when, with To; neither is any time. None on a day not free.</param>
/// <param name="To">Until when, after From.</param>
public record AvailabilityDay(DateOnly Date, bool Own, bool Free, TimeOnly? From, TimeOnly? To);

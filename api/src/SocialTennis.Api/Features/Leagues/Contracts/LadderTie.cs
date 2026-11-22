namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>Teams of one Division sharing a place that moves (#10), in ladder order.</summary>
public record LadderTie(int Division, IReadOnlyList<Guid> TeamIds);

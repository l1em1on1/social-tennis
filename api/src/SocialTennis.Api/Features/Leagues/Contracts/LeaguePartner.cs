namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>
/// A Club member a Player could ask to join a doubles League with (#6). Only a
/// name: email addresses are for joining the Club (owner, 2026-11-20).
/// </summary>
public record LeaguePartner(Guid PlayerId, string Name);

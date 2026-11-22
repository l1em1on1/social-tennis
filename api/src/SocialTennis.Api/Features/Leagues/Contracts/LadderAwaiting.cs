namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>
/// What waits on the Manager before the Stage can close (see CONTEXT.md,
/// Resolution Queue; #10): its Players see it too.
/// </summary>
/// <param name="UnscoredGameIds">Games with no Score that aren't marked not played: the Manager records or voids each.</param>
/// <param name="Ties">Ties that decide who moves, for the Manager to order at close.</param>
public record LadderAwaiting(IReadOnlyList<Guid> UnscoredGameIds, IReadOnlyList<LadderTie> Ties);

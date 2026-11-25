using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Contracts;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Users.Contracts;
using SocialTennis.Api.Roles;
using SocialTennis.Api.Validation;

namespace SocialTennis.Api.Features.Users;

/// <summary>
/// Users with their roles and Players, for an Admin granting roles: newest
/// first, one page at a time, optionally searched by email or Player name
/// (#134) and limited to a Club (#245). Every User used to come back in one response, which grew with the
/// Club until the Admin page was too slow to use.
/// </summary>
public static class GetUsers
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 100;

    /// <summary>A search longer than any email or Player name can't match anything.</summary>
    public const int MaxSearchLength = 320;

    /// <param name="q">Part of an email or of a Player's name, any case. Empty: everyone.</param>
    /// <param name="offset">How many matching Users to skip.</param>
    /// <param name="limit">How many to return, 1 to <see cref="MaxLimit"/>.</param>
    /// <param name="clubId">
    /// Only Users of this Club: with a Player in it or asking to be, a role there, or a League of its they
    /// run. One that isn't there matches nobody.
    /// </param>
    public static async Task<Results<Ok<GetUsersResponse>, ValidationProblem>> HandleAsync(
        TennisDbContext db,
        IOptions<AuthOptions> authOptions,
        [FromQuery] string? q = null,
        [FromQuery] int offset = 0,
        [FromQuery] int limit = DefaultLimit,
        [FromQuery] Guid? clubId = null,
        CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, string[]>();
        if (offset < 0)
        {
            errors["offset"] = ["The offset can't be negative."];
        }
        if (limit is < 1 or > MaxLimit)
        {
            errors["limit"] = [$"The limit is from 1 to {MaxLimit}."];
        }
        if (q is not null && (q.Length > MaxSearchLength || !TextRules.HasNoControlCharacters(q)))
        {
            errors["q"] = [$"Search with up to {MaxSearchLength} letters, numbers, spaces and punctuation."];
        }
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var matching = db.Users.AsQueryable();
        var search = q?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            // The search is a substring, not a pattern: its own % and _ match
            // themselves (Npgsql's ILike with an escape character).
            var pattern = $"%{EscapeLikePattern(search)}%";
            matching = matching.Where(u =>
                EF.Functions.ILike(u.Email, pattern, LikeEscape)
                || db.Players.Any(p => p.UserId == u.Id && EF.Functions.ILike(p.Name, pattern, LikeEscape)));
        }

        if (clubId is { } club)
        {
            matching = matching.Where(u =>
                db.ClubMemberships.Any(m => m.ClubId == club && m.Player.UserId == u.Id)
                || db.ClubRoles.Any(r => r.ClubId == club && r.UserId == u.Id)
                || db.LeagueManagers.Any(m => m.League.ClubId == club && m.UserId == u.Id));
        }

        var total = await matching.CountAsync(cancellationToken);
        // Newest first. A search that is someone's whole email puts them first,
        // so an exact address is never pushed off the page by newer Users whose
        // addresses merely contain it. Emails are stored lower-case.
        var exactEmail = search?.ToLowerInvariant();
        var ordered = string.IsNullOrEmpty(exactEmail)
            ? matching.OrderByDescending(u => u.CreatedAt)
            : matching.OrderByDescending(u => u.Email == exactEmail).ThenByDescending(u => u.CreatedAt);
        var rows = await ordered
            .ThenBy(u => u.Id)
            .Skip(offset)
            .Take(limit)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.Roles,
                Clubs = db.ClubRoles
                    .Where(r => r.UserId == u.Id)
                    .OrderBy(r => r.Club.Name)
                    .ThenBy(r => r.ClubId)
                    .Select(r => new UserClubRole(r.ClubId, r.Club.Name, r.Kind))
                    .ToList(),
                Players = db.Players
                    .Where(p => p.UserId == u.Id)
                    .OrderBy(p => p.CreatedAt)
                    .Select(p => new UserPlayer(p.Id, p.Name))
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        // Across every User, not just this page: whether an Admin is the last
        // one doesn't depend on what the Admin page is showing.
        var grantedAdmins = await db.Users.CountAsync(u => (u.Roles & UserRoles.Admin) == UserRoles.Admin, cancellationToken);
        var auth = authOptions.Value;
        var users = rows
            .Select(u =>
            {
                var granted = u.Roles.HasFlag(UserRoles.Admin);
                var bySetting = auth.IsAdminEmail(u.Email);
                var refusal = AdminRoleRules.CanRemoveAdmin(bySetting, auth.HasAdminEmails, grantedAdmins - (granted ? 1 : 0));
                return new UserSummary(
                    u.Id,
                    u.Email,
                    u.Clubs,
                    Admin: granted || bySetting,
                    AdminBySetting: bySetting,
                    LastAdmin: granted && refusal == AdminRoleRules.Refusal.LastAdmin,
                    u.Players);
            })
            .ToList();

        return TypedResults.Ok(new GetUsersResponse(users, new PageInfo(total, offset, limit)));
    }

    private const string LikeEscape = "\\";

    private static string EscapeLikePattern(string value) =>
        value.Replace(LikeEscape, LikeEscape + LikeEscape)
            .Replace("%", LikeEscape + "%")
            .Replace("_", LikeEscape + "_");
}
